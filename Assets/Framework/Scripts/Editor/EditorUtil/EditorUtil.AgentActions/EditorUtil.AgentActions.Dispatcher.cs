/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentActions.Dispatcher.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Nova Project C# Action 的计划、执行与验证调度器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentActions
        {
            /// <summary>
            /// 只读生成 Action 计划。只有 ready 计划会分配一次性 PlanId。
            /// </summary>
            public static async Task<AgentActionPlan> PlanAsync(
                string actionId,
                string requestJson,
                CancellationToken cancellationToken)
            {
                if (!AgentActionRuntime.IsMainThread)
                {
                    return CreatePlan(actionId, "blocked", "Agent Action 只能在 Unity 主线程计划。", null);
                }

                RegisteredAction action = Registry.Find(actionId);
                if (action == null)
                {
                    return CreatePlan(actionId, "not_applicable", "未注册的 Nova Project Action。", null);
                }
                long registryGeneration = Registry.Generation;

                if (action.Descriptor.RequiresStableEditor && (EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    return CreatePlan(actionId, "blocked", "Unity 正在编译或更新，Action 暂不可计划。", null);
                }

                if (action.Descriptor.RequiresEditMode && EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return CreatePlan(actionId, "blocked", "该 Action 只能在 Edit Mode 计划，请先退出或取消进入 Play Mode。", null);
                }

                object request;
                if (!action.Handler.TryParseRequest(requestJson, out request, out string requestError))
                {
                    return CreatePlan(actionId, "blocked", "Action 请求不符合严格契约：" + requestError, null);
                }

                if (!AgentActionLockManager.TryAcquire(action.Descriptor.Locks, out IDisposable lease))
                {
                    return CreatePlan(actionId, "blocked", "Action 所需资源当前正被其它 Action 使用。", null);
                }

                using (lease)
                {
                    try
                    {
                        AgentActionHandlerPlan handlerPlan = await action.Handler.PlanAsync(
                            request,
                            new AgentActionExecutionContext(null, cancellationToken));
                        if (!AgentActionRuntime.IsMainThread)
                        {
                            return CreatePlan(actionId, "blocked", "Action Handler 离开了 Unity 主线程，计划未保存。", null);
                        }
                        if (Registry.Generation != registryGeneration)
                        {
                            if (handlerPlan?.State is IDisposable staleState) staleState.Dispose();
                            return CreatePlan(actionId, "blocked", "Action Registry 在计划期间发生变化，请重新计划。", null);
                        }
                        if (action.Descriptor.RequiresEditMode && EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            if (handlerPlan?.State is IDisposable playModeState) playModeState.Dispose();
                            return CreatePlan(
                                actionId,
                                "blocked",
                                "Unity 在计划期间进入或准备进入 Play Mode，计划未保存。",
                                null);
                        }
                        if (handlerPlan == null)
                        {
                            return CreatePlan(actionId, "blocked", "Action Handler 未返回计划。", null);
                        }

                        if (handlerPlan.Status != "ready" && handlerPlan.Status != "blocked" &&
                            handlerPlan.Status != "not_applicable")
                        {
                            return CreatePlan(actionId, "blocked", "Action Handler 返回了非法 Plan 状态。", null);
                        }

                        AgentActionPlan plan = CreatePlan(actionId, handlerPlan.Status, handlerPlan.Summary, handlerPlan);
                        if (handlerPlan.Status == "ready")
                        {
                            var storedPlan = new AgentActionStoredPlan
                            {
                                Action = action,
                                HandlerState = handlerPlan.State,
                                RegistryGeneration = registryGeneration,
                            };
                            if (!AgentActionRuntime.PlanStore.TryAdd(
                                    storedPlan,
                                    out string planId,
                                    out DateTime expiresAtUtc))
                            {
                                return CreatePlan(actionId, "blocked", "Action 计划存储已达到容量上限，请等待旧计划失效。", null);
                            }

                            string recoveryReceiptJson = string.IsNullOrWhiteSpace(handlerPlan.RecoveryPayloadJson)
                                ? null
                                : Util.Json.Serialize(new AgentActionReceiptEnvelope
                                {
                                    ActionId = action.Descriptor.Id,
                                    ContractMajor = action.Descriptor.ContractMajor,
                                    PayloadJson = handlerPlan.RecoveryPayloadJson,
                                });
                            if (!AgentActionRuntime.OperationStore.TryCreate(
                                    action.Descriptor.Id,
                                    action.Descriptor.ContractMajor,
                                    registryGeneration,
                                    requestJson,
                                    handlerPlan.WriteSet,
                                    recoveryReceiptJson,
                                    out AgentActionOperationRecord operation,
                                    out string recoveryToken))
                            {
                                AgentActionRuntime.PlanStore.Remove(planId);
                                return CreatePlan(actionId, "blocked", "无法建立持久化 Operation，拒绝保存可执行计划。", null);
                            }

                            storedPlan.OperationId = operation.OperationId;
                            plan.PlanId = planId;
                            plan.ExpiresAtUtc = expiresAtUtc;
                            plan.OperationId = operation.OperationId;
                            plan.RecoveryToken = recoveryToken;
                        }
                        return plan;
                    }
                    catch (OperationCanceledException)
                    {
                        return CreatePlan(actionId, "blocked", "Action 计划已取消。", null);
                    }
                    catch (Exception exception)
                    {
                        return CreatePlan(actionId, "blocked", "Action 计划失败：" + exception.Message, null);
                    }
                }
            }

            /// <summary>
            /// 消费一次 ready 计划。要求确认的 Action 必须以当前 PlanId 作为确认令牌。
            /// </summary>
            public static async Task<AgentActionResult> ExecuteAsync(
                string planId,
                string confirmationToken,
                CancellationToken cancellationToken)
            {
                return await ExecuteAsync(null, planId, confirmationToken, cancellationToken);
            }

            /// <summary>
            /// 以预期 Action ID 原子消费一次 ready 计划。传输桥必须使用此入口，确保未开放
            /// Action 的计划在进入任何领域写操作前被拒绝。
            /// </summary>
            public static async Task<AgentActionResult> ExecuteAsync(
                string expectedActionId,
                string planId,
                string confirmationToken,
                CancellationToken cancellationToken)
            {
                if (!AgentActionRuntime.IsMainThread)
                {
                    return AgentActionResult.Create(null, "blocked", "Agent Action 只能在 Unity 主线程执行。");
                }

                if (!AgentActionRuntime.PlanStore.TryTake(planId, out AgentActionStoredPlan stored))
                {
                    return AgentActionResult.Create(null, "blocked", "计划不存在、已消费或已因 domain reload 失效。");
                }

                AgentActionDescriptor descriptor = stored.Action.Descriptor;
                try
                {
                    if (!string.IsNullOrWhiteSpace(expectedActionId) &&
                        !string.Equals(expectedActionId, descriptor.Id, StringComparison.Ordinal))
                    {
                        const string message = "计划所属 Action 与调用方预期不一致；已在写操作前拒绝执行。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }

                    if (stored.RegistryGeneration != Registry.Generation)
                    {
                        const string message = "Action Registry 已变化，旧计划失效。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }

                    if (descriptor.RequiresConfirmation && !string.Equals(planId, confirmationToken, StringComparison.Ordinal))
                    {
                        const string message = "确认令牌未与当前一次性 PlanId 绑定。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }

                    if (descriptor.RequiresStableEditor && (EditorApplication.isCompiling || EditorApplication.isUpdating))
                    {
                        const string message = "Unity 正在编译或更新，计划已失效，请重新计划。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }


                    if (descriptor.RequiresEditMode && EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        const string message = "该 Action 只能在 Edit Mode 执行，计划已失效，请退出或取消进入 Play Mode 后重新计划。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }

                    if (!AgentActionLockManager.TryAcquire(descriptor.Locks, out IDisposable lease))
                    {
                        const string message = "Action 所需资源正被其它 Action 使用，计划已失效。";
                        AgentActionRuntime.OperationStore.TryMarkBlocked(stored.OperationId, message);
                        return AgentActionResult.Create(descriptor.Id, "blocked", message);
                    }

                    using (lease)
                    {
                        if (!AgentActionRuntime.OperationStore.TryMarkExecuting(stored.OperationId))
                        {
                            return AgentActionResult.Create(
                                descriptor.Id,
                                "blocked",
                                "无法在写操作前持久化 executing 状态，已拒绝执行。");
                        }

                        try
                        {
                            AgentActionResult result = await stored.Action.Handler.ExecuteAsync(
                                stored.HandlerState,
                                new AgentActionExecutionContext(confirmationToken, cancellationToken));
                            if (!AgentActionRuntime.IsMainThread)
                            {
                                throw new InvalidOperationException("Action Handler 离开了 Unity 主线程。");
                            }

                            result = NormalizeResult(descriptor, result, "Execute");
                            if (!string.IsNullOrWhiteSpace(result.ReceiptJson))
                            {
                                result.ReceiptJson = Util.Json.Serialize(new AgentActionReceiptEnvelope
                                {
                                    ActionId = descriptor.Id,
                                    ContractMajor = descriptor.ContractMajor,
                                    PayloadJson = result.ReceiptJson,
                                });
                            }
                            result.RecoveryToken = AgentActionOperationStore.RecoveryTokenPrefix + stored.OperationId;
                            if (!AgentActionRuntime.OperationStore.TryCompleteExecution(stored.OperationId, result))
                            {
                                if (result.Status == "success") result.Status = "partial";
                                result.Warnings.Add("Action 已返回，但持久化 Operation 完成状态失败；不会自动重放。");
                            }
                            return result;
                        }
                        catch (OperationCanceledException)
                        {
                            const string message = "Action 执行已取消；不会自动重放。";
                            AgentActionRuntime.OperationStore.TryMarkInterrupted(stored.OperationId, message);
                            return CreateExecutionFailure(descriptor, message);
                        }
                        catch (Exception exception)
                        {
                            string message = "Action 执行失败；不会自动重放：" + exception.Message;
                            AgentActionRuntime.OperationStore.TryMarkInterrupted(stored.OperationId, message);
                            return CreateExecutionFailure(descriptor, message);
                        }
                    }
                }
                finally
                {
                    if (stored.HandlerState is IDisposable disposable) disposable.Dispose();
                }
            }

            /// <summary>
            /// 对 Receipt 做严格只读验证；不会因验证失败而重放 Execute。
            /// </summary>
            public static async Task<AgentActionResult> VerifyAsync(
                string actionId,
                string receiptJson,
                CancellationToken cancellationToken)
            {
                if (!AgentActionRuntime.IsMainThread)
                {
                    return AgentActionResult.Create(actionId, "blocked", "Agent Action 只能在 Unity 主线程验证。");
                }

                RegisteredAction action = Registry.Find(actionId);
                if (action == null)
                {
                    return AgentActionResult.Create(actionId, "not_applicable", "未注册的 Nova Project Action。");
                }

                if (string.IsNullOrWhiteSpace(receiptJson))
                {
                    return AgentActionResult.Create(actionId, "blocked", "Receipt 不能为空。");
                }

                string operationId = null;
                if (receiptJson.StartsWith(AgentActionOperationStore.RecoveryTokenPrefix, StringComparison.Ordinal))
                {
                    if (!AgentActionRuntime.OperationStore.TryLoad(
                            receiptJson,
                            actionId,
                            action.Descriptor.ContractMajor,
                            out AgentActionOperationRecord operation))
                    {
                        return AgentActionResult.Create(actionId, "blocked", "Operation recovery token 无效或与当前契约不匹配。");
                    }

                    operationId = operation.OperationId;
                    if (string.IsNullOrWhiteSpace(operation.ReceiptJson))
                    {
                        AgentActionResult unavailable = AgentActionResult.Create(
                            actionId,
                            "partial",
                            "Operation 已恢复，但没有可供领域 Verify 解释的 Receipt；不会重放 Execute。");
                        unavailable.RecoveryToken = receiptJson;
                        return unavailable;
                    }
                    receiptJson = operation.ReceiptJson;
                }

                AgentActionReceiptEnvelope envelope;
                try
                {
                    envelope = Util.Json.Deserialize<AgentActionReceiptEnvelope>(receiptJson);
                }
                catch (Exception exception)
                {
                    return AgentActionResult.Create(actionId, "blocked", "Receipt 信封无法解析：" + exception.Message);
                }

                if (envelope == null || envelope.ActionId != actionId ||
                    envelope.ContractMajor != action.Descriptor.ContractMajor ||
                    string.IsNullOrWhiteSpace(envelope.PayloadJson))
                {
                    return AgentActionResult.Create(actionId, "blocked", "Receipt 与当前 Action ID 或契约主版本不匹配。");
                }

                if (action.Descriptor.RequiresStableEditor && (EditorApplication.isCompiling || EditorApplication.isUpdating))
                {
                    return AgentActionResult.Create(actionId, "partial", "Unity 仍在编译或更新，暂不能完成验证。");
                }

                if (!AgentActionLockManager.TryAcquire(action.Descriptor.Locks, out IDisposable lease))
                {
                    return AgentActionResult.Create(actionId, "partial", "Action 所需资源当前正被使用，尚未完成验证。");
                }

                using (lease)
                {
                    try
                    {
                        AgentActionResult result = await action.Handler.VerifyAsync(
                            envelope.PayloadJson,
                            new AgentActionExecutionContext(null, cancellationToken));
                        if (!AgentActionRuntime.IsMainThread)
                        {
                            throw new InvalidOperationException("Action Verify Handler 离开了 Unity 主线程。");
                        }
                        result = NormalizeResult(action.Descriptor, result, "Verify");
                        result.ReceiptJson = receiptJson;
                        if (operationId != null)
                        {
                            result.RecoveryToken = AgentActionOperationStore.RecoveryTokenPrefix + operationId;
                            if (!AgentActionRuntime.OperationStore.TryRecordVerification(operationId, result))
                            {
                                result.Warnings.Add("Verify 已完成，但 Operation 验证状态持久化失败。");
                            }
                        }
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        return AgentActionResult.Create(actionId, "partial", "Action 验证已取消；不会自动重放。");
                    }
                    catch (Exception exception)
                    {
                        return AgentActionResult.Create(actionId, "partial", "Action 验证失败；不会自动重放：" + exception.Message);
                    }
                }
            }

            /// <summary>
            /// 无确认 Action 的单次快速入口。内部仍执行 Plan 与一次性消费，不开放任意 C# 方法调用。
            /// </summary>
            public static async Task<AgentActionResult> RunAsync(
                string actionId,
                string requestJson,
                CancellationToken cancellationToken)
            {
                if (!AgentActionRuntime.IsMainThread)
                {
                    return AgentActionResult.Create(actionId, "blocked", "Agent Action 只能在 Unity 主线程运行。");
                }

                RegisteredAction action = Registry.Find(actionId);
                if (action == null)
                {
                    return AgentActionResult.Create(actionId, "not_applicable", "未注册的 Nova Project Action。");
                }

                if (action.Descriptor.RequiresConfirmation)
                {
                    return AgentActionResult.Create(actionId, "blocked", "该 Action 必须先 Plan 并获得精确确认。");
                }

                AgentActionPlan plan = await PlanAsync(actionId, requestJson, cancellationToken);
                if (plan.Status != "ready")
                {
                    return AgentActionResult.Create(actionId, plan.Status, plan.Summary);
                }

                return await ExecuteAsync(plan.PlanId, null, cancellationToken);
            }

            private static AgentActionPlan CreatePlan(
                string actionId,
                string status,
                string summary,
                AgentActionHandlerPlan handlerPlan)
            {
                return new AgentActionPlan
                {
                    ActionId = actionId,
                    Status = status,
                    Summary = summary,
                    DataJson = handlerPlan?.DataJson,
                    WriteSet = handlerPlan?.WriteSet?.ToList() ?? new List<string>(),
                    Evidence = handlerPlan?.Evidence?.ToList() ?? new List<string>(),
                };
            }

            private static AgentActionResult NormalizeResult(
                AgentActionDescriptor descriptor,
                AgentActionResult result,
                string phase)
            {
                if (result == null)
                {
                    return AgentActionResult.Create(descriptor.Id, "partial", phase + " Handler 未返回结果。");
                }

                result.ActionId = descriptor.Id;
                if (result.Status != "success" && result.Status != "partial" &&
                    result.Status != "blocked" && result.Status != "not_applicable")
                {
                    result.Status = "partial";
                    result.Warnings.Add(phase + " Handler 返回了非法状态，已降级为 partial。");
                }

                if (result.Status == "success" &&
                    (result.EvidenceKinds & descriptor.RequiredEvidence) != descriptor.RequiredEvidence)
                {
                    result.Status = "partial";
                    result.Warnings.Add(
                        $"{phase} 尚未满足 Action 要求的证据集合：{descriptor.RequiredEvidence}。");
                }
                return result;
            }

            private static AgentActionResult CreateExecutionFailure(AgentActionDescriptor descriptor, string message)
            {
                AgentActionEffect writeEffects = AgentActionEffect.WorkspaceWrite |
                                                       AgentActionEffect.UnityWrite |
                                                       AgentActionEffect.ExternalWrite |
                                                       AgentActionEffect.BuildArtifact |
                                                       AgentActionEffect.Destructive;
                string status = (descriptor.Effects & writeEffects) == 0 ? "blocked" : "partial";
                return AgentActionResult.Create(descriptor.Id, status, message);
            }

        }
    }
}
