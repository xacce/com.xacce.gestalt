#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gestalt
{
    public class GestaltWindow : EditorWindow
    {
        enum Type
        {
            RegistrySingleton,
            ParallelJob,
        }

        private TextField _name;
        private Toggle _overrideExists;
        private Button _btn;
        private TextElement _status;
        private TextField _parentPath;
        private EnumField _type;

        [MenuItem("Window/CodeGen/Gestalt generator")]
        public static void Open()
        {
            var window = GetWindow<GestaltWindow>();
            window.Show();
        }

        public void CreateGUI()
        {
            _name = new TextField("Name");
            _type = new EnumField(Type.RegistrySingleton);
            _parentPath = new TextField("Parent Path");
            _parentPath.value = "Gestalts";
            _overrideExists = new Toggle("Override exists");
            _btn = new Button(Generate);
            _btn.text = "Generate";
            _status = new TextElement();
            rootVisualElement.Add(_name);
            rootVisualElement.Add(_type);
            rootVisualElement.Add(_parentPath);
            rootVisualElement.Add(_overrideExists);
            rootVisualElement.Add(_btn);
        }

        private void Generate()
        {
            if (string.IsNullOrEmpty(_name.value)) return;
            var path = Path.Combine(_parentPath.value, _name.value);
            var system = CreateSystem(_name.value);
            var authoring = CreateAuthoring(_name.value);
            var example = CreateExample(_name.value);
            WriteToFile(system, path, $"{_name.value}GestaltSystem.cs", _overrideExists.value);
            WriteToFile(authoring, path, $"{_name.value}GestaltAuthoring.cs", _overrideExists.value);
            WriteToFile(example, path, $"GameStartSo.cs", _overrideExists.value);
            EditorApplication.delayCall += AssetDatabase.Refresh;
        }

        private string CreateSystem(string nameValue)
        {
            switch (_type.value)
            {
                case Type.ParallelJob:
                {
                    return CreateParallelJobSystem(nameValue);
                }
                default:
                case Type.RegistrySingleton:
                {
                    return CreateRegistrySystem(nameValue);
                }
            }
        }

        private string ExampleStructRegistry(string name)
        {
            return $@"
 [PolymorphicStruct]
    public partial struct GameStart : I{name}Gestalt
    {{
        public int kek;
        public Pool.ObjectHandle next;

        public void OnUpdate(ref Pool.ObjectHandle current, EntityCommandBuffer ecb, {name}GestaltSystem.Q q, {name}GestaltSystem.Lookups lookups)
        {{
            Debug.Log($""Kek: {{kek}}"");
            kek++;
            current = next;
        }}
    }}
";
        }

        private string ExampleStructParallelJob(string name)
        {
            return $@"
 [PolymorphicStruct]
    public partial struct GameStart : I{name}Gestalt
    {{
        public int kek;
        public Pool.ObjectHandle next;

        public void OnUpdate(ref Pool.ObjectHandle current, EntityCommandBuffer.ParallelWriter ecb, {name}GestaltSystem.Context ctx, {name}GestaltSystem.Lookups lookups)
        {{
            Debug.Log($""Kek: {{kek}}"");
            kek++;
            current = next;
        }}
    }}
";
        }

        private string CreateExampleStruct(string name)
        {
            switch (_type.value)
            {
                case Type.ParallelJob:
                {
                    return ExampleStructParallelJob(name);
                }
                default:
                case Type.RegistrySingleton:
                {
                    return ExampleStructRegistry(name);
                }
            }
        }

        private string CreateExample(string name)
        {
            return @$"
using System;
using Trove;
using Trove.PolymorphicStructs;
using Unity.Entities;
using UnityEngine;

namespace Gestalt{name}
{{
    {CreateExampleStruct(name)}

    [CreateAssetMenu(menuName = ""Gestalts/{name}/GameStartSo"")]
    public class GameStartSo : Abstract{name}GestaltSo
    {{
        [SerializeField] private int kek;
        [SerializeField] private Abstract{name}GestaltSo next;


        public override Poly{name}Gestalt Bake(IBaker baker, Func<Abstract{name}GestaltSo, Pool.ObjectHandle> getHandle)
        {{
            return new GameStart()
            {{
                kek = kek,
                next = getHandle(next),
            }};
        }}
    }}
}}
";
        }


        private string CreateParallelJobSystem(string name)
        {
            return $@"
using AutoLookup;
using Trove;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
namespace Gestalt{name}
{{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    partial struct {name}GestaltSystem : ISystem
    {{
        private Lookups _lookups;
        private Context _ctx;

        [BurstCompile]
        [RequireMatchingQueriesForUpdate]
        partial struct Job : IJobEntity
        {{
            public EntityCommandBuffer.ParallelWriter ecb;
            public Context context;
            public Lookups lookups;

            [NativeSetThreadIndex] private int index;

            [BurstCompile]
            public void Execute(DynamicBuffer<{name}GestaltElement> steps, ref {name}GestaltCurrent current, Entity entity)
            {{
                var nullResult = new {name}GestaltElement();
                ref var currentStep = ref Pool.TryGetObjectRef(ref steps, current.current, out var success, ref nullResult);
                if (success)
                {{
                    currentStep.value.OnUpdate(ref current.current, ecb, context, lookups);
                }}
                else
                {{
                    ecb.DestroyEntity(index, entity);
                }}
            }}
        }}

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {{
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            _lookups = new Lookups(ref state);
        }}

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {{
        }}

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {{
            _lookups.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = new Job()
            {{
                ecb = ecb.AsParallelWriter(),
                context = new Context(),
                lookups = _lookups,
            }}.ScheduleParallel(state.Dependency);
        }}

        [AutoLookups]
        public partial struct Lookups
        {{
        }}

        public partial struct Context
        {{
        }}
    }}
}}
";
        }

        private string CreateRegistrySystem(string name)
        {
            return @$"
using AutoEntityQuery;
using AutoLookup;
using Trove;
using Unity.Burst;
using Unity.Entities;

namespace Gestalt{name}
{{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    partial struct {name}GestaltSystem : ISystem
    {{
        private Lookups _lookups;
        private Q _q;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {{
            state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
            _lookups = new Lookups(ref state);
            _q = new Q();
            _q.CreateDecoratedQueries(ref state);
        }}

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {{
        }}

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {{
            _lookups.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var entity = _q.entityQuery.GetSingletonEntity();
            var steps = _lookups.bufferRw[entity];
            ref var current = ref _lookups.currentRw.GetRefRW(entity).ValueRW;
            var nullResult = new {name}GestaltElement();
            ref var currentStep = ref Pool.TryGetObjectRef(ref steps, current.current, out var success, ref nullResult);
            if (success)
            {{
                currentStep.value.OnUpdate(ref current.current, ecb, _q, _lookups);
            }}
            else
            {{
                ecb.DestroyEntity(entity);
            }}
        }}

        [AutoLookups]
        public partial struct Lookups
        {{
            public BufferLookup<{name}GestaltElement> bufferRw;
            public ComponentLookup<{name}GestaltCurrent> currentRw;
        }}

        [AutoQueries]
        public partial struct Q
        {{
            [RequireForUpdateQ] [WithAllQ(typeof({name}GestaltElement), typeof({name}GestaltCurrent))]
            public EntityQuery entityQuery;
        }}
    }}
}}
";
        }

        private string CreateInterface(string name)
        {
            switch (_type.value)
            {
                case Type.ParallelJob:
                {
                    return @$"
[IsMergedFieldsPolymorphicStruct]
            [PolymorphicStructInterface]
        public interface I{name}Gestalt
        {{
            public void OnUpdate(ref Pool.ObjectHandle current, EntityCommandBuffer.ParallelWriter ecb, {name}GestaltSystem.Context ctx, {name}GestaltSystem.Lookups lookups);
        }}

";
                }
                default:
                case Type.RegistrySingleton:
                {
                    return @$"
[IsMergedFieldsPolymorphicStruct]
            [PolymorphicStructInterface]
        public interface I{name}Gestalt
        {{
            public void OnUpdate(ref Pool.ObjectHandle current, EntityCommandBuffer ecb, {name}GestaltSystem.Q q, {name}GestaltSystem.Lookups lookups);
        }}

";
                }
            }
        }

        public string CreateAuthoring(string name)
        {
            return @$"
using System;
using System.Collections.Generic;
using Trove;
using Trove.PolymorphicStructs;
using Unity.Entities;
using UnityEngine;

namespace Gestalt{name}
{{
    {CreateInterface(name)}


    public partial struct {name}GestaltElement: IBufferElementData, IPoolObject
    {{
        public int Version {{ get; set; }}
        public Poly{name}Gestalt value;
    }}

    public partial struct {name}GestaltCurrent : IComponentData
    {{
        public Pool.ObjectHandle current;
    }}

    public class {name}GestaltAuthoring : MonoBehaviour
    {{
        [SerializeField] Abstract{name}GestaltSo entry_s;

        class _ : Baker<{name}GestaltAuthoring>
        {{
            public override void Bake({name}GestaltAuthoring authoring)
            {{
                if (authoring.entry_s == null) return;
                var e = GetEntity(TransformUsageFlags.None);
                var b = AddBuffer<{name}GestaltElement>(e);
                Pool.Init(ref b, 0);
                var baker = new Poly{name}GestaltBaker(this, b);

                AddComponent(e, new {name}GestaltCurrent
                {{
                    current = baker.BakeEntry(authoring.entry_s),
                }});
            }}
        }}
    }}

    public class Poly{name}GestaltBaker
    {{
        private readonly Dictionary<Abstract{name}GestaltSo, Pool.ObjectHandle> baked = new Dictionary<Abstract{name}GestaltSo, Pool.ObjectHandle>();
        private DynamicBuffer<{name}GestaltElement> _buffer;
        private IBaker _baker;

        public Poly{name}GestaltBaker(IBaker baker, DynamicBuffer<{name}GestaltElement> buffer)
        {{
            _baker = baker;
            _buffer = buffer;
        }}

        public Pool.ObjectHandle BakeEntry(Abstract{name}GestaltSo entry)
        {{
            return GetHandle(entry);
        }}

        public IBaker baker => _baker;

        public Pool.ObjectHandle GetHandle(Abstract{name}GestaltSo step)
        {{
            if (step == null) return Pool.ObjectHandle.Null;
            if (baked.TryGetValue(step, out var handle)) return handle;
            var preBaked = new {name}GestaltElement();
            Pool.AddObject(ref _buffer, preBaked, out var preBakedHandle); //avoid loops and correct dedupl
            baked.Add(step, preBakedHandle);
            var data = new {name}GestaltElement
            {{
                value = step.Bake(_baker, GetHandle),
            }};
            Pool.TrySetObject(ref _buffer, preBakedHandle, data);

            return preBakedHandle;
        }}
    }}


    public abstract class Abstract{name}GestaltSo : ScriptableObject
    {{
        public abstract Poly{name}Gestalt Bake(IBaker baker, Func<Abstract{name}GestaltSo, Pool.ObjectHandle> getHandle);
    }}
}}
";
        }

        public static void WriteToFile(string content, string path, string filename, bool overr = true)
        {
            var dirPath = Path.Join(Application.dataPath, path);
            var savePath = Path.Join(dirPath, filename);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (File.Exists(savePath) && !overr) return;

            AssetDatabase.MakeEditable(savePath);
            File.WriteAllText(savePath, content);
        }
    }
}
#endif