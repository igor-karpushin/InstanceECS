using AnimationInstance.Scripts;
using Unity.Entities;
using Unity.Rendering;

namespace AnimationInstance.Ecs
{
    [MaterialProperty("_AnimationType", MaterialPropertyFormat.Float), GenerateAuthoringComponent]
    public struct AnimationTypeComponent : IComponentData
    {
        public float Value;
    }
}
