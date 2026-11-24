"""Read Solotopia release tags and match public UPM releases to a project snapshot."""

from collections import OrderedDict
from concurrent.futures import ThreadPoolExecutor, wait
from http.client import HTTPConnection, HTTPException, HTTPSConnection
import json
import re
import threading
import time
from urllib.parse import urlsplit

from protocol import KEY


REGISTRIES = {"public": "https://upm.solotopiax.com", "internal": "http://172.16.22.175:4874"}
UNITY_REGISTRY = "https://packages.unity.com"
APPLOVIN_REGISTRY = "https://unity.packages.applovin.com"
OPENUPM_REGISTRY = "https://package.openupm.com"
OPENUPM_SCOPES = ("com.coplaydev.unity-mcp", "com.google.external-dependency-manager")
MAX_CATALOG = 4 << 20
MAX_PACKAGE_CACHE = 1024
VERSION = re.compile(r"[a-zA-Z0-9][a-zA-Z0-9.+-]{0,79}\Z")
SEMVER = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
                    r"(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\Z")
UNITY_LINE = re.compile(r"([0-9]{1,6})\.([0-9]{1,5})\Z")
UNITY_RELEASE = re.compile(r"([0-9]{1,5})([abfp])([0-9]{1,5})(?:c[0-9]{1,5})?\Z")


def registry_json(url, path):
    """直接读取固定仓库的 JSON，限制连接和响应大小。"""
    origin = urlsplit(url)
    connection_type = HTTPSConnection if origin.scheme == "https" else HTTPConnection
    connection = connection_type(origin.hostname, origin.port, timeout=3)
    try:
        connection.request("GET", path, headers={"Accept": "application/json"})
        response = connection.getresponse()
        if response.status != 200:
            return None
        raw = response.read(MAX_CATALOG + 1)
        if len(raw) > MAX_CATALOG:
            return None
        data = json.loads(raw)
        return data if isinstance(data, dict) else None
    except (OSError, HTTPException, ValueError, RecursionError):
        return None
    finally:
        connection.close()


def fetch_catalog(url):
    """返回包名和 dist-tags.latest。请求失败与空仓库分别处理。"""
    data = registry_json(url, "/-/all")
    if data is None:
        return None
    versions = {}
    for name, entry in data.items():
        if not KEY.fullmatch(name) or not isinstance(entry, dict):
            continue
        tags = entry.get("dist-tags")
        latest = tags.get("latest") if isinstance(tags, dict) else None
        versions[name] = latest if isinstance(latest, str) and VERSION.fullmatch(latest) else None
    return versions


def unity_version(line, release):
    """比较 Unity 年份或技术版本号、补丁、发布阶段和修订号。"""
    major_minor = UNITY_LINE.fullmatch(line) if isinstance(line, str) else None
    patch = UNITY_RELEASE.fullmatch(release) if isinstance(release, str) else None
    if not major_minor or not patch:
        return None
    return (int(major_minor[1]), int(major_minor[2]), int(patch[1]), "abfp".index(patch[2]), int(patch[3]))


def package_version(value):
    """生成 SemVer 排序键。构建元数据不参与优先级。"""
    match = SEMVER.fullmatch(value) if isinstance(value, str) and len(value) <= 80 else None
    if not match:
        return None
    prerelease = tuple((0, int(part)) if part.isdecimal() else (1, part) for part in (match[4] or "").split(".") if part)
    return (int(match[1]), int(match[2]), int(match[3]), not prerelease, prerelease)


def fetch_releases(url, name):
    """保留包的兼容要求和版本顺序，不缓存整个仓库响应。"""
    data = registry_json(url, "/" + name)
    if not data or data.get("name") != name or not isinstance(data.get("versions"), dict):
        return None
    releases = {}
    for version, manifest in data["versions"].items():
        order = package_version(version)
        if not order or not isinstance(manifest, dict):
            continue
        if "unity" not in manifest:
            minimum = (0, 0, 0, 0, 0)
        else:
            release = manifest.get("unityRelease")
            minimum = unity_version(manifest["unity"], "0a0" if release in (None, "") else release)
        releases[version] = (order, minimum)
    return releases


def latest_compatible(releases, installed, editor):
    """确认上报版本属于该仓库后，寻找符合 Editor 要求且无需降级的最高版本。"""
    if not releases or installed not in releases:
        return None
    current = releases[installed][0]
    candidates = ((order, version == installed, version) for version, (order, minimum) in releases.items()
                  if minimum is not None and minimum <= editor and order >= current and (order[3] or not current[3]))
    return max(candidates, default=(None, False, None))[2]


def public_registry(name):
    if name.startswith("com.unity."):
        return UNITY_REGISTRY
    if name.startswith("com.applovin."):
        return APPLOVIN_REGISTRY
    if name in OPENUPM_SCOPES:
        return OPENUPM_REGISTRY
    return None


class RegistryVersions:
    def __init__(self):
        self.catalogs = {name: {} for name in REGISTRIES}
        self.expires = dict.fromkeys(REGISTRIES, 0)
        self.lock = threading.Lock()
        self.package_pool = ThreadPoolExecutor(max_workers=12)
        self.package_cache = OrderedDict()
        self.package_lock = threading.Lock()

    def close(self):
        self.package_pool.shutdown(cancel_futures=True)

    def for_packages(self, packages):
        """给本次项目出现过的 Solotopia 仓库包补充发布标签。"""
        if not packages:
            return {}
        with self.lock:
            expired = [name for name, expires in self.expires.items() if expires <= time.monotonic()]
            if expired:
                with ThreadPoolExecutor(max_workers=len(expired)) as pool:
                    for name, result in zip(expired, pool.map(fetch_catalog, (REGISTRIES[name] for name in expired))):
                        if result is None:
                            # 保留已知仓库归属，等待重查时不把上次结果当作最新版本。
                            self.catalogs[name] = dict.fromkeys(self.catalogs[name])
                        else:
                            self.catalogs[name] = result
                        self.expires[name] = time.monotonic() + (30 if result is None else 300)
            public, internal = self.catalogs["public"], self.catalogs["internal"]
            result = {}
            for name in packages:
                if name in public:
                    result[name] = {"registry": "public", "latest": public[name]}
                elif name in internal:
                    result[name] = {"registry": "internal", "latest": internal[name]}
                elif name.startswith("com.solotopia."):
                    result[name] = {"registry": "public", "latest": None}
            return result

    @staticmethod
    def fetch_package(url, name):
        result = fetch_releases(url, name) if name else fetch_catalog(url)
        return time.monotonic() + (300 if result is not None else 30), result

    def package_metadata(self, keys, deadline):
        """并发共享同一查询，保留超出本次响应时限的后台查询供后续读取。"""
        jobs = {}
        deferred = False
        with self.package_lock:
            for key in keys:
                job = self.package_cache.get(key)
                if job is not None and job.done() and job.result()[0] <= time.monotonic():
                    del self.package_cache[key]
                    job = None
                if job is None:
                    if len(self.package_cache) >= MAX_PACKAGE_CACHE:
                        disposable = next((k for k, cached in self.package_cache.items() if cached.done()), None)
                        if disposable is None:
                            deferred = True
                            continue
                        del self.package_cache[disposable]
                    job = self.package_pool.submit(self.fetch_package, *key)
                    self.package_cache[key] = job
                self.package_cache.move_to_end(key)
                jobs[key] = job
        done, pending = wait(jobs.values(), timeout=max(0, deadline - time.monotonic()))
        return {key: job.result()[1] for key, job in jobs.items() if job in done}, deferred or bool(pending)

    def for_snapshot(self, snapshot):
        """给一份环境快照补查适用于该 Unity 版本的第三方注册包。"""
        packages = snapshot["packages"]
        result = self.for_packages({item["id"] for item in packages})
        line, _, release = snapshot["unity"].rpartition(".")
        editor = unity_version(line, release)
        if editor is None:
            return result, False

        lookups, unclassified = {}, []
        for item in packages:
            name = item["id"]
            if name in result or item["source"] != "registry" or not package_version(item["version"]):
                continue
            registry = public_registry(name)
            if registry:
                lookups[(registry, name)] = item["version"]
            else:
                unclassified.append(item)

        # 先在公开的 OpenUPM 索引内匹配，避免把无法确认归属的项目包名发往外部。
        index = (OPENUPM_REGISTRY, None)
        keys = [*lookups, index] if unclassified else list(lookups)
        deadline = time.monotonic() + 4
        metadata, pending = self.package_metadata(keys, deadline)
        if metadata.get(index):
            openupm = {(OPENUPM_REGISTRY, item["id"]): item["version"] for item in unclassified if item["id"] in metadata[index]}
            found, more_pending = self.package_metadata(openupm, deadline)
            lookups.update(openupm)
            metadata.update(found)
            pending |= more_pending

        for key, installed in lookups.items():
            latest = latest_compatible(metadata.get(key), installed, editor)
            if latest:
                result[key[1]] = {"registry": "third-party", "latest": latest}
        return result, pending
