The main goal is to simplify the creation of fundamental game loop code while staying within `ISystem` and `Burst`.

In the end, each stage of the game is described in the following way:

```csharp
    [PolymorphicStruct]
    public partial struct GameStart : IPt2Gestalt
    {
        public int kek;
        public Pool.ObjectHandle next;

        public void OnUpdate(ref Pool.ObjectHandle current, EntityCommandBuffer.ParallelWriter ecb, Pt2GestaltSystem.Context ctx, Pt2GestaltSystem.Lookups lookups)
        {
            Debug.Log($"Kek: {kek}");
            kek++;
            current = next;
        }
    }


    [CreateAssetMenu(menuName = "Gestalts/Pt2/GameStartSo")]
    public class GameStartSo : AbstractPt2GestaltSo
    {
        [SerializeField] private int kek;
        [SerializeField] private AbstractPt2GestaltSo next;


        public override PolyPt2Gestalt Bake(IBaker baker, Func<AbstractPt2GestaltSo, Pool.ObjectHandle> getHandle)
        {
            return new GameStart()
            {
                kek = kek,
                next = getHandle(next),
            };
        }
    }

```
Here, GameStart is a polymorphic struct and has a callback from the generated system.
The So variant is for serializing context data required for transitions of some internal logic.

It requires AutoLookups, AutoEntityQuery.
It does not contain Roslyn code generation.
All code generation is needed only to create the initial template of Authorings, interfaces, and the system.

Generation window: Window/CodeGen/Gestalt generator