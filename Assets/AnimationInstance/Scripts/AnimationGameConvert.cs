using AnimationInstance.Ecs;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace AnimationInstance.Scripts
{
    [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
    public class AnimationGameConvert : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            var query = DstEntityManager.CreateEntityQuery(
                typeof(RenderMesh),
                typeof(Parent));
            var entities = query.ToEntityArray(Allocator.Persistent);
            foreach (var entity in entities)
            {
                var render = DstEntityManager.GetSharedComponentData<RenderMesh>(entity);
                render.receiveShadows = false;
                DstEntityManager.SetSharedComponentData(entity, render);
                
                var parent = DstEntityManager.GetComponentData<Parent>(entity);
                if (DstEntityManager.HasComponent<AnimationTypeComponent>(parent.Value))
                {
                    var animation = DstEntityManager.GetComponentData<AnimationTypeComponent>(parent.Value);
                    var offset = DstEntityManager.GetComponentData<AnimationOffsetComponent>(parent.Value);
                    DstEntityManager.AddComponentData(entity, animation);
                    DstEntityManager.AddComponentData(entity, offset);
                }
            }
            entities.Dispose();
        }
    }
}
