using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Charasiew.ECS
{
    public struct EnemySpawnData : IComponentData
    {
        public Entity enemyPrefab;
        public float spawnInterval;         // 生成间隔
        public float spawnDistance;
    }

    public struct EnemySpawnState : IComponentData
    {
        public float spawnTimer;
        public Random random;
    }
    
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public float spawnInterval;
        public float spawnDistance;
        public uint randomSeed;
        
        private class Baker: Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemySpawnData
                {
                    enemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                    spawnInterval = authoring.spawnInterval,
                    spawnDistance = authoring.spawnDistance
                });
                AddComponent(entity, new EnemySpawnState
                {
                    spawnTimer = 0.0f,
                    random = Random.CreateFromIndex(authoring.randomSeed)
                });
            }
        }
    }
    
    public partial struct EnemySpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPositon = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            
            foreach (var (spawnState, spawnData) in SystemAPI.Query<RefRW<EnemySpawnState>, RefRO<EnemySpawnData>>())
            {
                spawnState.ValueRW.spawnTimer -= deltaTime;
                if (spawnState.ValueRO.spawnTimer > 0.0f)
                    continue;
                spawnState.ValueRW.spawnTimer = spawnData.ValueRO.spawnInterval;

                var newEnemy = ecb.Instantiate(spawnData.ValueRO.enemyPrefab);
                //TODO 元宝
                #region 元宝 
                var spawnAngle = spawnState.ValueRW.random.NextFloat(0.0f, math.TAU);
                var spawnDir = new float3()
                {
                    x = math.sin(spawnAngle),
                    y = math.cos(spawnAngle),
                    z = 0,
                };
                #endregion
                var spawnPoint = spawnDir * spawnData.ValueRO.spawnDistance + playerPositon;
                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPoint));
                
            }
        }
    }
}