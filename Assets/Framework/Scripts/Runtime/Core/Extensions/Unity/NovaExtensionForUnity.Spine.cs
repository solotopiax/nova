/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Spine.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Spine
 *            提供对 Spine SkeletonAnimation 和 SkeletonGraphic 的槽位替换与动画时长获取扩展操作
 ***************************************************************/

#if NOVA_SPINE
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
#endif
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Unity Spine 的扩展方法集合。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
#if NOVA_SPINE
        /// <summary>
        /// 修改 SkeletonAnimation 中槽位的皮肤精灵。
        /// </summary>
        /// <param name="skeletonAnimation"><see cref="SkeletonAnimation"/> 对象。</param>
        /// <param name="slotName">槽位名称。</param>
        /// <param name="findSpriteName">要替换的精灵名称。</param>
        /// <param name="sprite">替换用的 Sprite。</param>
        /// <param name="skinLayerName">皮肤所在层名称。</param>
        public static void ChangeSlotSkinSprite(this SkeletonAnimation skeletonAnimation, string slotName, string findSpriteName, Sprite sprite, string skinLayerName)
        {
            SpinChangeSlotSkinSprite(skeletonAnimation.Skeleton, skeletonAnimation.skeletonDataAsset, skeletonAnimation.AnimationState, slotName, findSpriteName, sprite, skinLayerName);
        }

        /// <summary>
        /// 修改 SkeletonGraphic 中槽位的皮肤精灵。
        /// </summary>
        /// <param name="skeletonGraphic"><see cref="SkeletonGraphic"/> 对象。</param>
        /// <param name="slotName">槽位名称。</param>
        /// <param name="findSpriteName">要替换的精灵名称。</param>
        /// <param name="sprite">替换用的 Sprite。</param>
        /// <param name="skinLayerName">皮肤所在层名称。</param>
        public static void ChangeSlotSkinSprite(this SkeletonGraphic skeletonGraphic, string slotName, string findSpriteName, Sprite sprite, string skinLayerName)
        {
            skeletonGraphic.allowMultipleCanvasRenderers = true;
            SpinChangeSlotSkinSprite(skeletonGraphic.Skeleton, skeletonGraphic.skeletonDataAsset, skeletonGraphic.AnimationState, slotName, findSpriteName, sprite, skinLayerName);
        }

        /// <summary>
        /// Spine 的槽位皮肤精灵替换核心实现。
        /// </summary>
        /// <param name="skeleton"><see cref="Skeleton"/> 对象。</param>
        /// <param name="skeletonDataAsset">Skeleton 数据资源。</param>
        /// <param name="animationState">AnimationState 对象。</param>
        /// <param name="slotName">槽位名称。</param>
        /// <param name="findSpriteName">要替换的精灵名称。</param>
        /// <param name="sprite">替换用的 Sprite。</param>
        /// <param name="skinLayerName">皮肤所在层名称。</param>
        private static void SpinChangeSlotSkinSprite(Skeleton skeleton, SkeletonDataAsset skeletonDataAsset, Spine.AnimationState animationState, string slotName, string findSpriteName, Sprite sprite, string skinLayerName)
        {
            Attachment cloneAttachment;
            var skeletonData = skeletonDataAsset.GetSkeletonData(true);
            var skin = skeletonData.FindSkin(skinLayerName);
            var slotData = skeletonData.FindSlot(slotName);
            Attachment templateAttachment = skin.GetAttachment(slotData.Index, findSpriteName);
            cloneAttachment = templateAttachment.GetRemappedClone(sprite, templateAttachment.GetMaterial());
            skin.SetAttachment(slotData.Index, slotName, cloneAttachment);
            skeleton.SetSkin(skin);
            skeleton.SetSlotsToSetupPose();
            animationState.Apply(skeleton);
        }

        /// <summary>
        /// 获取 SkeletonGraphic 中指定动画的播放时长。
        /// </summary>
        /// <param name="skeletonGraphic"><see cref="SkeletonGraphic"/> 对象。</param>
        /// <param name="animationName">动画名称。</param>
        /// <returns>动画播放时长，若动画不存在则返回 0。</returns>
        public static float GetSpineDuration(this SkeletonGraphic skeletonGraphic, string animationName)
        {
            return GetSpineDurationBySkeletonDataAsset(skeletonGraphic.skeletonDataAsset, animationName);
        }

        /// <summary>
        /// 获取 SkeletonAnimation 中指定动画的播放时长。
        /// </summary>
        /// <param name="skeletonAnimation"><see cref="SkeletonAnimation"/> 对象。</param>
        /// <param name="animationName">动画名称。</param>
        /// <returns>动画播放时长，若动画不存在则返回 0。</returns>
        public static float GetSpineDuration(this SkeletonAnimation skeletonAnimation, string animationName)
        {
            return GetSpineDurationBySkeletonDataAsset(skeletonAnimation.skeletonDataAsset, animationName);
        }

        /// <summary>
        /// 通过 SkeletonDataAsset 获取指定动画的播放时长。
        /// </summary>
        /// <param name="skeletonDataAsset"><see cref="SkeletonDataAsset"/> 对象。</param>
        /// <param name="animationName">动画名称。</param>
        /// <returns>动画播放时长，若动画不存在则返回 0。</returns>
        private static float GetSpineDurationBySkeletonDataAsset(SkeletonDataAsset skeletonDataAsset, string animationName)
        {
            var skeletonData = skeletonDataAsset.GetSkeletonData(false);
            var animation = skeletonData.FindAnimation(animationName);
            return animation != null ? animation.Duration : 0f;
        }
#endif
    }
}
