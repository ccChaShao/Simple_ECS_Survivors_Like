using Unity.Entities;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct PlasmaBlastData : IComponentData
    {
        public float moveSpeed;
        public int attackDamge;
    }
    public class PlasmaBlastAuthoring : MonoBehaviour
    {
        public float moveSpeed;
        public int attackDamge;
        
        private class Baker : Baker<PlasmaBlastAuthoring>
        {
            public override void Bake(PlasmaBlastAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlasmaBlastData
                {
                    moveSpeed = authoring.moveSpeed,
                    attackDamge = authoring.attackDamge,
                });
            }
        }
    }
}