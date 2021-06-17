using Unity.Entities;
using Unity.Rendering;

namespace AnimationInstance.Ecs
{
    [MaterialProperty("_PixelCountPerFrame", MaterialPropertyFormat.Float), GenerateAuthoringComponent]
    public struct MaterialPixelCountComponent : IComponentData
    {
        public float Value;
    }
}
