using AnimationInstance.Ecs;
using AnimationInstance.Scripts;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[ExecuteAlways]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public class MaterialSetupSystem : SystemBase
{
    EndInitializationEntityCommandBufferSystem m_CommandBufferSystem;
    protected override void OnCreate()
    {
        m_CommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
    }
    
    protected override void OnUpdate()
    {
        var commandBuffer = m_CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(0, 99999));
        Entities
            .WithBurst(FloatMode.Default, FloatPrecision.Standard, true)
            .WithAll<MaterialSetupData, Child>()
            .ForEach((Entity entity, int entityInQueryIndex, in DynamicBuffer<Child> child, in MaterialSetupData data) =>
            {
                commandBuffer.RemoveComponent<MaterialSetupData>(entityInQueryIndex, entity);
                var randomFloat = random.NextFloat(0f, 2f);
                var secondAnimation = random.NextFloat(0f, 100f) < 60f ? AnimationType.Idle : AnimationType.Idle1;
                for (var i = 0; i < child.Length; ++i)
                {
                    commandBuffer.AddComponent(entityInQueryIndex + i, child[i].Value, new MaterialPixelStartComponent { Value = data.PixelStart });
                    commandBuffer.AddComponent(entityInQueryIndex + i, child[i].Value, new MaterialPixelCountComponent { Value = data.PixelCount });
                    commandBuffer.AddComponent(entityInQueryIndex + i, child[i].Value, new AnimationOffsetComponent { Value = randomFloat });
                    commandBuffer.AddComponent(entityInQueryIndex + i, child[i].Value, new AnimationTypeComponent { Value = (float)secondAnimation });
                }
                
            }).ScheduleParallel();
        
        m_CommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}
