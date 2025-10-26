using TMG.Survivors;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct DestroyEntityFlag : IComponentData, IEnableableComponent { }
    
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]          // 需要在结束模拟之前执行，因为要将指令列表挂靠在结束模拟系统上；
    public partial struct DestoryEntitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 一个 Unity ECS 内置的系统，它的关键特性是在一帧的模拟阶段（Simulation Phase）的最后、呈现阶段（Presentation Phase）开始之前，执行所有提交给它的命令。
            // 这样做可以将本帧内所有需要进行的结构性变更（如销毁实体）收集起来，批量处理，减少性能开销。
            var endEcbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            // 从上面获取的系统中创建了一个具体的 EntityCommandBuffer。
            // 你可以把它想象成一个“待办事项列表”。在当前帧（OnUpdate方法中），我们只是把想要执行的销毁命令“记录”在这个列表上，而不是立即执行。
            // 将这个“待办事项列表”记录在模拟阶段系统中。
            var endEcb = endEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            // 初始化缓冲区
            var beginEcbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var beginEcb = beginEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            // 默认情况下，当组件的启用状态（enable）为 false时，SystemAPI.Query是查不出该实体的。
            foreach (var (_, entity) in SystemAPI.Query<RefRO<DestroyEntityFlag>>().WithEntityAccess())
            {
                // 玩家死亡
                if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    GameUIController.Instance.ShowGameOverUI();
                }
                // 宝石奖励死亡
                if (SystemAPI.HasComponent<GemPrefab>(entity))
                {
                    var gemPrefab = SystemAPI.GetComponent<GemPrefab>(entity).value;
                    var newGem = beginEcb.Instantiate(gemPrefab);
                    var spawnPosition = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
                    beginEcb.SetComponent(newGem, LocalTransform.FromPosition(spawnPosition));
                }
                endEcb.DestroyEntity(entity);
            }
        }
    }
}