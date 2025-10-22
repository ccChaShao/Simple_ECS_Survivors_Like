using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    /// <summary>
    /// 动画下标
    /// </summary>
    public enum PlayerAnimationIndex : byte
    {
        Movement = 0,
        Idle = 1,
        None = byte.MaxValue,
    }
    
    public struct PlayerTag : IComponentData { }
    
    public struct InititalizeCameraTargetTag : IComponentData { }

    public struct CameraTarget : IComponentData
    {
        public UnityObjectRef<Transform> cameraTransform;           // ecs引用unity对象的类型
    }

    [MaterialProperty("_AnimationIndex")]
    public struct AnimationIndexOverride : IComponentData
    {
        public float value;
    }
    
    [RequireComponent(typeof(CharacterAuthoring))]
    public class PlayerAuthoring : MonoBehaviour
    {
        private class Baker: Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
                AddComponent<InititalizeCameraTargetTag>(entity);
                AddComponent<CameraTarget>(entity);
                AddComponent<AnimationIndexOverride>(entity);
            }
        }
    }

    /// <summary>
    /// 相机初始化系统
    /// （放在初始化系统组，保证在比较前的位置执行）
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CameraInititalizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InititalizeCameraTargetTag>();           // 当世界中存在至少一个具备指定组件的实体时，系统的OnUpdate才会执行；
        }

        public void OnUpdate(ref SystemState state)
        {
            if (CameraTargetSingleton.Instance == null)
            {
                return;
            }

            Transform cameraTargetTransform = CameraTargetSingleton.Instance.transform;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (cameraTarget, entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InititalizeCameraTargetTag, PlayerTag>().WithEntityAccess())
            {
                cameraTarget.ValueRW.cameraTransform = cameraTargetTransform;
                ecb.RemoveComponent<InititalizeCameraTargetTag>(entity);            // 这里移除掉组件（结构性改变，所以使用ecb进行延迟性批量改动）
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// 相机移动系统
    /// （放在transform系统组中更新）
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct MoveCameraSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (localToWorld, cameraTarget) in SystemAPI.Query<RefRO<LocalToWorld>, RefRW<CameraTarget>>().WithAll<PlayerTag>().WithNone<InititalizeCameraTargetTag>())         // 需要玩家标签，并且已经初始化完毕了
            {
                cameraTarget.ValueRW.cameraTransform.Value.position = localToWorld.ValueRO.Position;
            }
        }
    }
    
    public partial class PlayerInputSystem : SystemBase
    {
        public SurvivorsInput input;

        protected override void OnCreate()
        {
            input = new();
            input.Enable();
        }

        protected override void OnUpdate()
        {
            var curInput = (float2)input.Player.Move.ReadValue<Vector2>();
            foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())
            {
                direction.ValueRW.value = curInput;
            }
        }
    }
}
