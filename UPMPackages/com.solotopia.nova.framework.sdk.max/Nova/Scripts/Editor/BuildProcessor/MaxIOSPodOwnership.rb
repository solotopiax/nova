# Nova 自有 MAX iOS 依赖归属修复。由固定 C# 回调在 pod 的 Ruby 启动阶段加载并退出。
# 输入只有已导出的工程根目录与两个官方 XML 路径，不接受代码、命令或任意依赖配置。
# Ruby 宿主和 xcodeproj 使用有效 CocoaPods 启动器的环境，不固定机器安装路径。
begin
  require 'json'
  require 'rexml/document'
  require 'xcodeproj'

  root = File.realpath(ENV.fetch('NOVA_MAX_IOS_EXPORT_ROOT'))
  project_path = File.join(root, 'Unity-iPhone.xcodeproj')
  pbx_path = File.join(project_path, 'project.pbxproj')
  podfile_path = File.join(root, 'Podfile')
  raise '生成工程缺少 Unity-iPhone.xcodeproj/project.pbxproj 或 Podfile' unless File.file?(pbx_path) && File.file?(podfile_path)

  # 固定两个官方版本的 XML 契约；未来升级必须重新审查，不能自动沿用旧版本的 Pods 归属决策。
  adapters = [
    {xml: ENV.fetch('NOVA_MAX_IOS_LINE_XML'), pod: 'AppLovinMediationLineAdapter', version: '3.1.1.2',
     url: 'https://github.com/AppLovin/AppLovin-MAX-Swift-Package-Line.git', spm: '3010102.0.0'},
    {xml: ENV.fetch('NOVA_MAX_IOS_UNITYADS_XML'), pod: 'AppLovinMediationUnityAdsAdapter', version: '4.20.0.1',
     url: 'https://github.com/AppLovin/AppLovin-MAX-Swift-Package-UnityAds.git', spm: '4200001.0.0'}
  ]
  adapters.each do |adapter|
    document = REXML::Document.new(File.binread(adapter[:xml]))
    pods = REXML::XPath.match(document, '/dependencies/iosPods/iosPod')
    packages = REXML::XPath.match(document, '/dependencies/remoteSwiftPackage')
    products = REXML::XPath.match(document, '/dependencies/remoteSwiftPackage/swiftPackage')
    valid = pods.size == 1 && packages.size == 1 && products.size == 1 &&
      pods.first.attributes['name'] == adapter[:pod] && pods.first.attributes['version'] == adapter[:version] &&
      packages.first.attributes['url'] == adapter[:url] && packages.first.attributes['version'] == adapter[:spm] &&
      products.first.attributes['name'] == adapter[:pod] && products.first.attributes['replacesPod'] == adapter[:pod] &&
      products.first.attributes['target'] == 'All'
    raise "官方 MAX XML 契约漂移：#{adapter[:pod]}" unless valid
  end

  original_pbx = File.binread(pbx_path)
  original_podfile = File.binread(podfile_path)
  project = Xcodeproj::Project.open(project_path)
  targets = project.targets.select { |target| target.name == 'UnityFramework' }
  raise '生成工程必须包含唯一 UnityFramework target' unless targets.size == 1
  target = targets.first
  object_types = Xcodeproj::Project::Object
  repositories = project.objects.grep(object_types::XCRemoteSwiftPackageReference)
  unexpected = repositories.select do |repository|
    repository.repositoryURL.start_with?('https://github.com/AppLovin/AppLovin-MAX-Swift-Package') &&
      adapters.none? { |adapter| adapter[:url] == repository.repositoryURL }
  end
  raise '生成工程包含未审查的 MAX SPM 仓库，不能确保 AppLovinSDK 唯一归属' unless unexpected.empty?

  # 先完整验证引用图和 Podfile，再做任何写入；禁止删除未知 product、target 或 build phase 引用。
  removals = []
  adapters.each do |adapter|
    matches = repositories.select { |repository| repository.repositoryURL == adapter[:url] }
    raise "MAX SPM 仓库重复：#{adapter[:pod]}" if matches.size > 1
    repository = matches.first
    products = project.objects.grep(object_types::XCSwiftPackageProductDependency).select do |product|
      product.product_name == adapter[:pod] || (repository && product.package == repository)
    end
    if repository
      raise "MAX SPM 版本或归属漂移：#{adapter[:pod]}" unless
        repository.requirement == {'kind' => 'exactVersion', 'version' => adapter[:spm]} &&
        project.root_object.package_references.include?(repository) &&
        products.size == 1 && products.first.product_name == adapter[:pod] && products.first.package == repository
      product = products.first
      raise "MAX product 不属于唯一 UnityFramework target：#{adapter[:pod]}" unless
        target.package_product_dependencies.include?(product)
      builds = project.objects.grep(object_types::PBXBuildFile).select { |build| build.product_ref == product }
      allowed_product_referrers = [target] + builds
      raise "MAX product 存在未审查引用：#{adapter[:pod]}" unless
        (product.referrers - allowed_product_referrers).empty? &&
        (repository.referrers - [project.root_object, product]).empty?
      builds.each do |build|
        raise "MAX build file 存在未审查 phase：#{adapter[:pod]}" unless
          build.referrers.size == 1 && target.build_phases.include?(build.referrers.first)
      end
      removals.concat(builds + [product, repository])
    else
      raise "MAX product 存在孤立引用：#{adapter[:pod]}" unless products.empty?
    end
  end

  lines = original_podfile.lines
  starts = lines.each_index.select { |index| lines[index].match?(/^target\s+['"]UnityFramework['"]\s+do\s*$/) }
  raise 'Podfile 必须包含唯一顶层 UnityFramework target' unless starts.size == 1
  start_index = starts.first
  end_index = ((start_index + 1)...lines.size).find { |index| lines[index].match?(/^end\s*$/) }
  raise 'Podfile UnityFramework target 缺少结束行' unless end_index
  body = lines[(start_index + 1)...end_index]
  raise 'Podfile UnityFramework target 包含未审查的嵌套结构' unless body.all? do |line|
    line.match?(/^\s*(?:#.*)?$/) || line.match?(/^\s*pod\s+['"][^'"]+['"].*$/)
  end
  additions = []
  adapters.each do |adapter|
    matches = lines.each_index.select { |index| lines[index].match?(/^\s*pod\s+['"]#{Regexp.escape(adapter[:pod])}['"](?:\s|,|$)/) }
    raise "Podfile MAX Pod 重复或位于其他 target：#{adapter[:pod]}" unless
      matches.size <= 1 && matches.all? { |index| index > start_index && index < end_index }
    if matches.empty?
      newline = lines[start_index].end_with?("\r\n") ? "\r\n" : "\n"
      additions << "  pod '#{adapter[:pod]}', '#{adapter[:version]}'#{newline}"
    else
      raise "Podfile MAX Pod 版本漂移：#{adapter[:pod]}" unless lines[matches.first].match?(
        /^\s*pod\s+['"]#{Regexp.escape(adapter[:pod])}['"]\s*,\s*['"]#{Regexp.escape(adapter[:version])}['"]\s*(?:#.*)?$/)
    end
  end
  lines.insert(start_index + 1, *additions)
  updated_podfile = lines.join

  # 根据已允许的 UUID 删除建立预期完整工程；序列化前后比较所有对象，确保 Firebase 和其他配置不变。
  expected = Marshal.load(Marshal.dump(project.to_hash))
  removed_ids = removals.map(&:uuid)
  removed_ids.each { |uuid| expected.fetch('objects').delete(uuid) }
  expected.fetch('objects').each_value do |object|
    ['packageReferences', 'packageProductDependencies', 'files'].each do |key|
      object[key] = object[key].reject { |uuid| removed_ids.include?(uuid) } if object[key].is_a?(Array)
    end
  end
  removals.each(&:remove_from_project)
  raise 'MAX 修改影响了允许范围以外的工程对象' unless project.to_hash == expected

  changed = !removals.empty? || updated_podfile != original_podfile
  if changed
    begin
      project.save unless removals.empty?
      File.binwrite(podfile_path, updated_podfile) if updated_podfile != original_podfile
      raise 'MAX 工程回写复核失败' unless Xcodeproj::Project.open(project_path).to_hash == expected &&
        File.binread(podfile_path) == updated_podfile
    rescue StandardError
      File.binwrite(pbx_path, original_pbx)
      File.binwrite(podfile_path, original_podfile)
      raise
    end
  end

  puts 'NOVA_MAX_IOS_POD_OWNERSHIP:' + JSON.generate({changed: changed, removed_object_ids: removed_ids,
    pods: adapters.to_h { |adapter| [adapter[:pod], adapter[:version]] },
    retained_spm_urls: project.root_object.package_references.map(&:repositoryURL)})
  # 此脚本由 RUBYOPT 加载；这里退出可证明不会继续执行 CocoaPods CLI，也不会重复 pod install。
  exit(0)
rescue StandardError => error
  warn "MAX iOS 依赖归属修复失败：#{error.class}: #{error.message}"
  exit(1)
end
