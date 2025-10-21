using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct PlayerTag : IComponentData { }
    
    public struct InititalizeCameraTargetTag : IComponentData { }

    public struct CameraTarget : IComponentData
    {
        public UnityObjectRef<Transform> cameraTransform;           // ecs引用unity对象的类型
    }
    
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
