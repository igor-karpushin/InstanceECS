
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace AnimationInstance.Ecs
{

    public struct MaterialSetupData : IComponentData
    {
        public int PixelStart;
        public int PixelCount;
    }
    
    public class AnimationConvertComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        public int PixelStart = default;
        public int PixelCount = default;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            //dstManager.RemoveComponent<LocalToWorld>(entity);
            dstManager.AddComponentData(entity, new MaterialSetupData
            {
                PixelCount = PixelCount,
                PixelStart = PixelStart
            });
        }
    }
}
