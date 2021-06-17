using Unity.Entities;
using Unity.Rendering;

namespace AnimationInstance.Ecs
{
    [MaterialProperty("_PixelStart", MaterialPropertyFormat.Float), GenerateAuthoringComponent]
    public struct MaterialPixelStartComponent : IComponentData
    {
        public float Value;
    }
}
