using Unity.Entities;
using Unity.Rendering;

namespace AnimationInstance.Ecs
{
    [MaterialProperty("_AnimOffset", MaterialPropertyFormat.Float), GenerateAuthoringComponent]
    public struct AnimationOffsetComponent : IComponentData
    {
        public float Value;
    }
}
