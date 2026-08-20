using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace PummelCustomItems
{
    /// <summary>Everything the registry needs to know to add one board item.</summary>
    internal sealed class CustomItemDef
    {
        public ulong Id;                     // MUST be 0..255 - see ItemRegistry.MAX_ID
        public string Name;
        public string Description;
        public string PrefabName;            // net prefab name, spawned when the item is equipped
        public Type Behaviour;               // an Item subclass
        public bool SkipTurnAfterUse;

        // Visuals. Replace the placeholders with real models once the asset pipeline is up.
        public Func<GameObject> MakeReceiveModel;   // shown flying at you when you obtain it
        public Func<GameObject> MakeHeldModel;      // held in hand while equipped; may be null

        public Vector3 HeldPosition = Vector3.zero;
        public Vector3 HeldRotation = Vector3.zero;
        public Vector3 HeldScale = Vector3.one;
        public PlayerBone HeldBone = PlayerBone.LeftHand;

        // Size of the model in the "you got an item" popup. ItemDetails has no scale field
        // for it, so we bake the size into the model copy itself. Kept here rather than in
        // the bundle so tuning is a two-second mod rebuild, not a Unity run.
        public float ReceiveScale = 1f;

        public Vector3 ReceiveRotation = Vector3.zero;

        // Bundle asset used purely to render the inventory icon. It must be a SINGLE-mesh
        // model with the viewing angle baked in: PummelTargetRenderer ignores transforms
        // and re-frames the camera per MeshFilter, so a multi-part model comes out framed
        // on whichever part is last. Leave null to fall back to the receive model.
        public string IconAsset;

        // How large the icon is drawn in the inventory. Vanilla items use 61, but their
        // art fills the texture edge to edge; our rendered icons sit inside a margin the
        // preview camera leaves, so they need a bigger box to look the same size.
        public float IconSize = 82f;

        // Weapon spaces draw from a SEPARATE pool - only items with this flag. Vanilla marks
        // its four supers; ours had none, so disabling the vanilla items emptied that pool
        // and those spaces started handing out nothing at all.
        public bool WeaponSpace;

        // Power grade, 1 (strongest) to 4 (weakest), mirroring how the game grades its own
        // minigame rewards: better placements draw from stronger tables. Ignored for supers,
        // which never appear as a minigame reward.
        public int Tier = 3;
    }

    /// <summary>
    /// A networked object that is not an item - a projectile, a trap, anything the game
    /// has to spawn by name. Registered the same way, just without ItemDetails.
    /// </summary>
    internal sealed class ExtraPrefabDef
    {
        public string Name;
        public Type Behaviour;
        public Func<GameObject> BuildBody;   // visual + physics, no networking
    }

    /// <summary>
    /// Temporary stand-in art. These are scaffolding so the logic can be built and tested
    /// before real models exist - an item is not finished while it still uses one.
    /// </summary>
    internal static class Placeholders
    {
        internal static GameObject Primitive(PrimitiveType type, Color color, float scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "PCI_Placeholder";
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = color;
                r.material = m;
            }
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }

    internal static class ItemRegistry
    {
        // itemID must fit in a byte: GameBoardController.ClickedItem() casts it before
        // queueing ActionEquipItem and DoAction() looks the item back up by that value.
        // Vanilla uses 0..14; anything above 255 wraps and hangs the board.
        internal const ulong FIRST_CUSTOM_ID = 15;
        internal const ulong MAX_ID = 255;

        private const string HOLDER_NAME = "PCI_PrefabHolder";

        private static readonly List<CustomItemDef> s_defs = new List<CustomItemDef>();
        private static readonly List<ExtraPrefabDef> s_extras = new List<ExtraPrefabDef>();
        private static readonly Dictionary<string, GameObject> s_planted =
            new Dictionary<string, GameObject>();
        private static readonly Dictionary<ulong, ItemDetails> s_details =
            new Dictionary<ulong, ItemDetails>();

        private static bool s_netTypesParsed;

        // ------------------------------------------------------------------ catalogue

        static ItemRegistry()
        {
            s_defs.Add(new CustomItemDef
            {
                Id = FIRST_CUSTOM_ID + 0,
                Name = "Шаффл игроков",
                Description = "Все игроки случайным образом меняются местами на доске.",
                PrefabName = "PCI_ShuffleItem",
                Behaviour = typeof(ShuffleItem),
                SkipTurnAfterUse = false,
                MakeReceiveModel = () => ModAssets.ModelOrPlaceholder("ShuffleOrb",
                    () => Placeholders.Primitive(PrimitiveType.Sphere, new Color(0.4f, 0.2f, 0.9f), 0.6f)),
                MakeHeldModel = () => ModAssets.ModelOrPlaceholder("ShuffleOrb",
                    () => Placeholders.Primitive(PrimitiveType.Sphere, new Color(0.4f, 0.2f, 0.9f), 0.25f)),
                HeldScale = Vector3.one * 0.9f,
                ReceiveScale = 2.6f,
                IconAsset = "ShuffleOrbIcon",
                WeaponSpace = true,
            });

            s_defs.Add(new CustomItemDef
            {
                Id = FIRST_CUSTOM_ID + 1,
                Name = "Заряженный кубик",
                Description = "Бросок на следующем твоём ходу будет максимальным.",
                PrefabName = "PCI_LoadedDiceItem",
                Behaviour = typeof(LoadedDiceItem),
                SkipTurnAfterUse = false,
                MakeReceiveModel = () => ModAssets.ModelOrPlaceholder("LoadedDie",
                    () => Placeholders.Primitive(PrimitiveType.Cube, Color.white, 0.5f)),
                MakeHeldModel = () => ModAssets.ModelOrPlaceholder("LoadedDie",
                    () => Placeholders.Primitive(PrimitiveType.Cube, Color.white, 0.25f)),
                HeldScale = Vector3.one * 1.0f,
                ReceiveScale = 2.8f,
                IconAsset = "LoadedDieIcon",
                Tier = 2,
            });

            s_defs.Add(new CustomItemDef
            {
                Id = FIRST_CUSTOM_ID + 2,
                Name = "Банановая бомба",
                Description = "Бросок по направлению. Банан отскакивает, взрывается и разлетается на пять бананчиков поменьше.",
                PrefabName = "PCI_I_Banana",
                Behaviour = typeof(BananaBombItem),
                SkipTurnAfterUse = false,
                MakeReceiveModel = () => ModAssets.ModelOrPlaceholder("Banana",
                    () => Placeholders.Primitive(PrimitiveType.Capsule, Color.yellow, 0.5f)),
                MakeHeldModel = () => ModAssets.ModelOrPlaceholder("Banana",
                    () => Placeholders.Primitive(PrimitiveType.Capsule, Color.yellow, 0.3f)),
                // The banana mesh is a full unit long, so in a hand it has to be small and
                // pushed clear of the palm - at 0.9 it swallowed the arm.
                HeldScale = Vector3.one * 0.22f,
                HeldPosition = new Vector3(0.06f, 0.03f, 0.04f),
                HeldRotation = new Vector3(0f, 100f, 20f),
                // Three-quarter view: seen end-on the curve vanishes and it reads as a barrel.
                ReceiveScale = 1.5f,
                ReceiveRotation = new Vector3(25f, 35f, 15f),
                IconAsset = "BananaIcon",
                Tier = 1,
            });

            AddThrownItem(FIRST_CUSTOM_ID + 3, "Осколочная граната",
                "Взрывается быстро и разлетается девятью осколками.",
                "PCI_I_Grenade", typeof(FragGrenadeItem), "Grenade",
                heldScale: 0.55f, receiveScale: 2.0f, tier: 1);

            AddThrownItem(FIRST_CUSTOM_ID + 4, "Липучка",
                "Прилипает к тому, в кого попала, и взрывается на нём.",
                "PCI_I_Sticky", typeof(StickyBombItem), "Sticky",
                heldScale: 0.5f, receiveScale: 2.0f, tier: 1);

            AddThrownItem(FIRST_CUSTOM_ID + 5, "Сосулька",
                "Летит прямо. Небольшой урон, но цель пропускает свой следующий бросок.",
                "PCI_I_Icicle", typeof(IcicleItem), "Icicle",
                heldScale: 0.6f, receiveScale: 2.2f, tier: 2);

            AddThrownItem(FIRST_CUSTOM_ID + 6, "Бумеранг",
                "Улетает по дуге и возвращается, задевая всех на пути дважды.",
                "PCI_I_Boomerang", typeof(BoomerangItem), "Boomerang",
                heldScale: 0.5f, receiveScale: 2.0f, tier: 2);

            AddThrownItem(FIRST_CUSTOM_ID + 7, "Рикошет",
                "Шесть дробин веером. Каждая отскакивает от препятствий до трёх раз — можно достать того, кто стоит за углом или за укрытием.",
                "PCI_I_Ricochet", typeof(RicochetItem), "Ricochet",
                heldScale: 0.8f, receiveScale: 2.2f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 8, "Обмен инвентарями",
                "Меняешься всеми предметами со случайным игроком.",
                "PCI_I_SwapInv", typeof(SwapInventoryItem), "SwapBag", 0.7f, 2.4f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 9, "Ксерокс",
                "Копирует один случайный предмет из твоего инвентаря.",
                "PCI_I_Copier", typeof(CopierItem), "Copier", 0.7f, 2.4f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 10, "Барахолка",
                "Переплавляет все твои предметы в один случайный.",
                "PCI_I_Junk", typeof(JunkShopItem), "Junk", 0.65f, 2.2f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 11, "Налог",
                "Забирает ключи у самого богатого из соперников и делит их поровну между всеми остальными, включая тебя.",
                "PCI_I_Tax", typeof(TaxItem), "Tax", 0.7f, 2.4f, tier: 1);

            AddSimpleItem(FIRST_CUSTOM_ID + 12, "Стеклянные пушки",
                "Твой следующий ход и весь круг до него: любой урон убивает с одного удара. Всех, включая тебя.",
                "PCI_I_Glass", typeof(GlassCannonsItem), "GlassCannon", 0.7f, 2.4f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 13, "Пиньята",
                "Твой следующий ход и весь круг до него: из раненых сыплются ключи.",
                "PCI_I_Pinata", typeof(PinataRoundItem), "Pinata", 0.65f, 2.2f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 14, "Щедрость",
                "Твой следующий ход и весь круг до него: предметы не расходуются. У всех.",
                "PCI_I_Generosity", typeof(GenerosityItem), "Generosity", 0.7f, 2.4f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 15, "Жезл смерти",
                "Пятьдесят на пятьдесят: убивает всех остальных — или тебя одного.",
                "PCI_I_DeathWand", typeof(DeathWandItem), "DeathWand", 0.8f, 2.2f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 16, "Армагеддон",
                "Метеоритный дождь: 9–11 урона всем на доске, включая тебя.",
                "PCI_I_Armageddon", typeof(ArmageddonItem), "MeteorRock", 0.7f, 2.4f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 17, "Пылесос",
                "Втягивает ключи в выбранном направлении, включая твои собственные.",
                "PCI_I_Vacuum", typeof(VacuumItem), "Vacuum", 0.7f, 2.2f, tier: 2);

            AddSimpleItem(FIRST_CUSTOM_ID + 18, "Копилка",
                "Твой следующий ход и весь круг до него: выбитые из других ключи достаются тебе.",
                "PCI_I_Piggy", typeof(PiggyBankItem), "Piggy", 0.7f, 2.4f, tier: 2);

            AddSimpleItem(FIRST_CUSTOM_ID + 19, "Проклятие",
                "Наводится на игрока. Его следующий бросок будет единицей.",
                "PCI_I_Curse", typeof(CurseItem), "Curse", 0.8f, 2.2f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 20, "Заморозка",
                "Наводится на игрока. Его следующий бросок будет нулём — ход впустую.",
                "PCI_I_Freeze", typeof(FreezeItem), "Freeze", 0.8f, 2.4f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 21, "Двойной ход",
                "Твой следующий бросок считается вдвое.",
                "PCI_I_DoubleMove", typeof(DoubleMoveItem), "DoubleDice", 0.8f, 2.4f, weaponSpace: true);

            AddSimpleItem(FIRST_CUSTOM_ID + 22, "Крюк-кошка",
                "Наводится на игрока и подтягивает его почти вплотную к тебе.",
                "PCI_I_Grapple", typeof(GrappleItem), "Grapple", 0.8f, 2.4f, tier: 2);

            AddSimpleItem(FIRST_CUSTOM_ID + 23, "Пинок",
                "Наводится на игрока и отбрасывает его на четыре клетки назад.",
                "PCI_I_Kick", typeof(KickItem), "Boot", 0.8f, 2.2f, tier: 2);

            AddSimpleItem(FIRST_CUSTOM_ID + 24, "Билет на старт",
                "Наводится на игрока и отправляет его в начало доски.",
                "PCI_I_Ticket", typeof(StartTicketItem), "Ticket", 0.7f, 2.2f, tier: 1);

            AddSimpleItem(FIRST_CUSTOM_ID + 25, "Мина",
                "Оставляет мину на твоей клетке. Взрывается у любого, кто на неё наступит, кроме тебя.",
                "PCI_I_Mine", typeof(MineItem), "Mine", 0.8f, 2.4f, tier: 3);

            AddSimpleItem(FIRST_CUSTOM_ID + 26, "Порча клетки",
                "Превращает клетку впереди в опасную.",
                "PCI_I_Poison", typeof(PoisonNodeItem), "Poison", 0.8f, 2.4f, tier: 4);

            AddSimpleItem(FIRST_CUSTOM_ID + 27, "Обманный указатель",
                "Твой следующий ход и весь круг до него: на развилках всех уводит не туда, куда они выбрали.",
                "PCI_I_Signpost", typeof(FakeSignpostItem), "Signpost", 0.8f, 2.4f, tier: 4);

            AddSimpleItem(FIRST_CUSTOM_ID + 28, "Магнит жизни",
                "Наводится на игрока. Высасывает 30% его здоровья и отдаёт их тебе.",
                "PCI_I_LifeMagnet", typeof(LifeMagnetItem), "LifeMagnet", 0.8f, 2.2f, tier: 1);

            s_extras.Add(new ExtraPrefabDef
            {
                Name = TrapPrefabs.Mine,
                Behaviour = typeof(LandMine),
                // Sits still on a node, so no rigidbody and no collider. Scaled up because
                // at its authored size it was barely visible against a board space.
                BuildBody = () =>
                {
                    GameObject body = ModAssets.ModelOrPlaceholder("Mine",
                        () => Placeholders.Primitive(PrimitiveType.Cylinder, Color.grey, 0.4f));
                    if (body != null) body.transform.localScale = Vector3.one * 2.2f;
                    return body;
                },
            });

            // Projectiles. All of them share one component; only the model and the spec
            // passed to Launch() differ.
            AddProjectile(Prefabs.Banana,    "Banana",    0.85f, 0.22f, 0.45f);
            AddProjectile(Prefabs.Grenade,   "Grenade",   1.0f,  0.20f, 0.15f);
            AddProjectile(Prefabs.Shard,     "Shard",     1.0f,  0.09f, 0.30f);
            AddProjectile(Prefabs.Sticky,    "Sticky",    1.0f,  0.22f, 0.05f);
            AddProjectile(Prefabs.Icicle,    "Icicle",    1.0f,  0.15f, 0.35f);
            AddProjectile(Prefabs.Pellet,    "Pellet",    1.0f,  0.09f, 0.75f);

            s_extras.Add(new ExtraPrefabDef
            {
                Name = Prefabs.Boomerang,
                Behaviour = typeof(Boomerang),
                // Driven along a scripted path, so no rigidbody and no collider.
                BuildBody = () => ModAssets.ModelOrPlaceholder("Boomerang",
                    () => Placeholders.Primitive(PrimitiveType.Cube, new Color(0.55f, 0.35f, 0.16f), 0.5f)),
            });
        }

        /// <summary>Registers an item that just fires and finishes - no aiming, no projectile.</summary>
        private static void AddSimpleItem(ulong id, string name, string description,
                                          string prefabName, Type behaviour, string model,
                                          float heldScale, float receiveScale,
                                          bool weaponSpace = false, int tier = 3)
        {
            AddThrownItem(id, name, description, prefabName, behaviour, model,
                          heldScale, receiveScale, weaponSpace, tier);
        }

        /// <summary>Registers one aim-and-throw item; they differ only in numbers.</summary>
        private static void AddThrownItem(ulong id, string name, string description,
                                          string prefabName, Type behaviour, string model,
                                          float heldScale, float receiveScale,
                                          bool weaponSpace = false, int tier = 3)
        {
            s_defs.Add(new CustomItemDef
            {
                Id = id,
                Name = name,
                Description = description,
                PrefabName = prefabName,
                Behaviour = behaviour,
                SkipTurnAfterUse = false,
                MakeReceiveModel = () => ModAssets.ModelOrPlaceholder(model,
                    () => Placeholders.Primitive(PrimitiveType.Sphere, Color.grey, 0.4f)),
                MakeHeldModel = () => ModAssets.ModelOrPlaceholder(model,
                    () => Placeholders.Primitive(PrimitiveType.Sphere, Color.grey, 0.25f)),
                HeldScale = Vector3.one * heldScale,
                HeldPosition = new Vector3(0.05f, 0.02f, 0.04f),
                ReceiveScale = receiveScale,
                IconAsset = model + "Icon",
                WeaponSpace = weaponSpace,
                Tier = tier,
            });
        }

        /// <summary>A physics projectile: bundle model plus the body it needs to fly.</summary>
        private static void AddProjectile(string prefabName, string model,
                                          float scale, float radius, float bounciness)
        {
            s_extras.Add(new ExtraPrefabDef
            {
                Name = prefabName,
                Behaviour = typeof(ThrownProjectile),
                BuildBody = () =>
                {
                    GameObject body = ModAssets.ModelOrPlaceholder(model,
                        () => Placeholders.Primitive(PrimitiveType.Sphere, Color.grey, 0.3f));
                    if (body == null) return null;

                    body.transform.localScale = Vector3.one * scale;

                    SphereCollider col = body.AddComponent<SphereCollider>();
                    col.radius = radius;

                    PhysicMaterial mat = new PhysicMaterial("PCI_" + model);
                    mat.bounciness = bounciness;
                    mat.dynamicFriction = 0.4f;
                    mat.staticFriction = 0.4f;
                    mat.bounceCombine = PhysicMaterialCombine.Maximum;
                    col.material = mat;

                    Rigidbody rb = body.AddComponent<Rigidbody>();
                    rb.mass = 1f;
                    rb.drag = ProjectileSpec.LinearDrag;
                    rb.angularDrag = 0.35f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    return body;
                },
            });
        }

        /// <summary>
        /// Our items that may turn up as a minigame reward: registered, enabled in the
        /// ruleset, and not a super - vanilla's reward tables list only its ordinary items.
        /// </summary>
        internal static List<ItemDetails> MinigameEligible(int tier)
        {
            List<ItemDetails> exact = new List<ItemDetails>();
            List<ItemDetails> any = new List<ItemDetails>();
            List<int> tiers = new List<int>();

            foreach (CustomItemDef def in s_defs)
            {
                if (def.WeaponSpace) continue;   // supers are never a minigame reward

                ItemDetails d;
                if (!s_details.TryGetValue(def.Id, out d) || d == null) continue;
                if (d.itemIndex == 255) continue;

                try { if (!d.GetIsActive()) continue; } catch { continue; }

                any.Add(d);
                tiers.Add(def.Tier);
                if (def.Tier == tier) exact.Add(d);
            }

            if (exact.Count > 0) return exact;
            if (any.Count == 0) return any;

            // Nothing at that exact grade - widen to the closest grade that has something,
            // so a disabled item never turns a placement into "no reward".
            int best = int.MaxValue;
            for (int i = 0; i < tiers.Count; i++)
                best = Mathf.Min(best, Mathf.Abs(tiers[i] - tier));

            List<ItemDetails> near = new List<ItemDetails>();
            for (int i = 0; i < any.Count; i++)
                if (Mathf.Abs(tiers[i] - tier) == best) near.Add(any[i]);

            return near;
        }

        /// <summary>Ids of every custom item, for debug tooling.</summary>
        internal static List<ulong> AllIds()
        {
            List<ulong> ids = new List<ulong>();
            foreach (CustomItemDef def in s_defs) ids.Add(def.Id);
            return ids;
        }

        internal static GameObject GetPlantedPrefab(string name)
        {
            GameObject go;
            if (s_planted.TryGetValue(name, out go) && go != null) return go;
            return null;
        }

        // -------------------------------------------------------------------- prefabs

        /// <summary>
        /// Builds every custom prefab and parks it under WorkshopController.prefabRoot.
        /// Must run before SetupNetPrefabs() scans that root.
        /// </summary>
        internal static void EnsurePrefabs()
        {
            WorkshopController wc = WorkshopController.Instance;
            if (wc == null || wc.prefabRoot == null)
            {
                Core.Warn("prefabRoot unavailable - cannot plant custom prefabs yet");
                return;
            }

            ParseNetTypes();

            Transform holderT = wc.prefabRoot.Find(HOLDER_NAME);
            GameObject holder;
            if (holderT != null)
            {
                holder = holderT.gameObject;
            }
            else
            {
                holder = new GameObject(HOLDER_NAME);
                // Deactivate BEFORE parenting so anything built inside never runs Awake.
                holder.SetActive(false);
                holder.transform.SetParent(wc.prefabRoot, false);
            }

            foreach (CustomItemDef def in s_defs)
            {
                try { EnsureOne(def, holder); }
                catch (Exception e) { Core.Warn("prefab build failed for " + def.Name + ": " + e); }
            }

            foreach (ExtraPrefabDef ex in s_extras)
            {
                try { EnsureExtra(ex, holder); }
                catch (Exception e) { Core.Warn("prefab build failed for " + ex.Name + ": " + e); }
            }
        }

        private static void EnsureExtra(ExtraPrefabDef ex, GameObject holder)
        {
            Transform alive = holder.transform.Find(ex.Name);
            if (alive != null)
            {
                s_planted[ex.Name] = alive.gameObject;
                return;
            }

            GameObject go = ex.BuildBody != null ? ex.BuildBody() : new GameObject();
            if (go == null)
            {
                Core.Warn("body builder returned null for " + ex.Name);
                return;
            }

            go.name = ex.Name;
            go.transform.SetParent(holder.transform, false);
            if (ex.Behaviour != null) go.AddComponent(ex.Behaviour);

            s_planted[ex.Name] = go;
            Core.Log("planted net prefab '" + ex.Name + "'");
        }

        private static void EnsureOne(CustomItemDef def, GameObject holder)
        {
            Transform alive = holder.transform.Find(def.PrefabName);
            if (alive != null)
            {
                s_planted[def.PrefabName] = alive.gameObject;
                return;
            }

            GameObject go = new GameObject(def.PrefabName);
            go.transform.SetParent(holder.transform, false);

            Component c = go.AddComponent(def.Behaviour);
            Item item = c as Item;
            if (item == null)
            {
                Core.Warn(def.Behaviour.Name + " is not an Item subclass");
                return;
            }

            // The item reads its own metadata through this back-reference, and the inventory
            // is decremented through it - without it the wrong item would be consumed.
            item.details = GetOrCreateDetails(def);

            s_planted[def.PrefabName] = go;
            Core.Log("planted net prefab '" + def.PrefabName + "'");
        }

        /// <summary>
        /// Our Item subclasses are new NetBehaviour types; the netcode has to learn them
        /// or it cannot serialise the spawned object.
        /// </summary>
        private static void ParseNetTypes()
        {
            if (s_netTypesParsed) return;

            List<Type> types = new List<Type>();
            foreach (CustomItemDef def in s_defs)
            {
                if (def.Behaviour != null && !types.Contains(def.Behaviour)) types.Add(def.Behaviour);
            }
            foreach (ExtraPrefabDef ex in s_extras)
            {
                if (ex.Behaviour != null && !types.Contains(ex.Behaviour)) types.Add(ex.Behaviour);
            }
            if (types.Count == 0) return;

            try
            {
                ZP.Net.NetSystem.ParseTypes(types.ToArray());
                s_netTypesParsed = true;
                Core.Log("registered " + types.Count + " net type(s)");
            }
            catch (Exception e)
            {
                Core.Warn("ParseTypes failed: " + e);
            }
        }

        // -------------------------------------------------------------------- details

        private static ItemDetails GetOrCreateDetails(CustomItemDef def)
        {
            ItemDetails d;
            if (s_details.TryGetValue(def.Id, out d) && d != null) return d;

            d = ScriptableObject.CreateInstance<ItemDetails>();
            d.hideFlags = HideFlags.HideAndDontSave;   // survives scene loads, never serialised

            d.enabled = true;
            d.enumReference = Items.HealthKit;   // only read by the mod editor's give-item mask
            d.itemID = def.Id;
            d.weaponSpaceItem = def.WeaponSpace;
            d.skipTurnAfterUse = def.SkipTurnAfterUse;

            // No localisation entries exist for these; TranslatedName falls back to the
            // token itself, so the token is the display text.
            d.itemNameToken = def.Name;
            d.descriptionToken = def.Description;

            d.netPrefabName = def.PrefabName;
            d.prefabPath = "";        // resolved by Patch_GetNetPrefab, not by Addressables
            d.prefab = null;

            // GiveItemEffects() instantiates recievePrefab unconditionally - a null here
            // throws the moment the item is handed out.
            d.recievePrefab = MakePersistent(def.MakeReceiveModel, def.PrefabName + "_Receive");
            if (d.recievePrefab != null)
            {
                if (def.ReceiveScale != 1f)
                    d.recievePrefab.transform.localScale *= def.ReceiveScale;
                if (def.ReceiveRotation != Vector3.zero)
                    d.recievePrefab.transform.localRotation = Quaternion.Euler(def.ReceiveRotation);
            }
            d.recievePrefabPath = "";
            d.rotateSpeed = 100f;

            d.heldPrefab = MakePersistent(def.MakeHeldModel, def.PrefabName + "_Held");
            d.heldPrefabPath = "";
            d.heldBone = def.HeldBone;
            d.heldPosition = def.HeldPosition;
            d.heldRotation = def.HeldRotation;
            d.heldScale = def.HeldScale;

            d.iconSize = new Vector2(def.IconSize, def.IconSize);
            d.inputHelp = MakeInputHelp(aiming: true);
            d.usingInputHelp = MakeInputHelp(aiming: false);

            EnsureIcon(d, IconModelFor(def) ?? d.recievePrefab);

            // Blank prompts are silent failures, so make the resolved text visible.
            Core.Log("prompt keys for '" + def.Name + "': Use Item -> \"" +
                     I2.Loc.LocalizationManager.GetTranslation("Use Item") +
                     "\", Back -> \"" + I2.Loc.LocalizationManager.GetTranslation("Back") + "\"");

            s_details[def.Id] = d;
            return d;
        }

        private static GameObject s_modelAttic;

        /// <summary>
        /// Parks a model out of sight but leaves it ACTIVE, because Instantiate() copies
        /// activeSelf: deactivating the source produced invisible held/receive models.
        /// The attic itself is inactive, which hides everything inside it.
        /// </summary>
        private static GameObject MakePersistent(Func<GameObject> factory, string name)
        {
            if (factory == null) return null;
            GameObject go = factory();
            if (go == null) return null;
            go.name = name;

            if (s_modelAttic == null)
            {
                s_modelAttic = new GameObject("PCI_ModelAttic");
                s_modelAttic.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(s_modelAttic);
            }
            go.transform.SetParent(s_modelAttic.transform, false);
            go.SetActive(true);
            return go;
        }

        /// <summary>The single-mesh model reserved for icon rendering, if the item has one.</summary>
        private static GameObject IconModelFor(CustomItemDef def)
        {
            if (string.IsNullOrEmpty(def.IconAsset)) return null;
            GameObject src = ModAssets.Prefab(def.IconAsset);
            if (src == null) return null;

            GameObject copy = UnityEngine.Object.Instantiate(src);
            copy.name = def.IconAsset;
            copy.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(copy);
            return copy;
        }

        /// <summary>
        /// Renders an icon from the item's model using the game's own item-preview renderer -
        /// the same one behind the mod editor's "Generate Icon" button. Without this the
        /// inventory shows a blank white square.
        /// </summary>
        private static void EnsureIcon(ItemDetails d, GameObject model)
        {
            if (d.iconTexture != null || d.icon != null || model == null) return;

            try
            {
                string dir = Path.Combine(
                    Path.Combine(Directory.GetCurrentDirectory(), "UserData"),
                    Path.Combine("PummelCustomItems", "icons"));
                Directory.CreateDirectory(dir);
                // Scale is part of the name: resizing the model must invalidate the cache,
                // otherwise the icon keeps showing the old proportions forever.
                Vector3 e = model.transform.localRotation.eulerAngles;
                string tag = Mathf.RoundToInt(model.transform.localScale.x * 100f) + "_" +
                             Mathf.RoundToInt(e.x) + "-" + Mathf.RoundToInt(e.y) + "-" + Mathf.RoundToInt(e.z);
                // Rendered icons are cached to disk, so anything that changes how a model
                // LOOKS - not just its size - has to invalidate them. Bumping this is how
                // the magenta icons baked before the shader rebind get thrown away.
                const int IconCacheVersion = 2;

                string file = Path.Combine(dir,
                    model.name + "_v" + IconCacheVersion + "_" + tag + ".png");

                if (!File.Exists(file))
                {
                    // The renderer needs a live object; ours lives in the inactive attic.
                    GameObject shot = UnityEngine.Object.Instantiate(model);
                    shot.SetActive(true);
                    try { PummelTargetRenderer.Render(file, 256, 256, new GameObject[] { shot }); }
                    finally { UnityEngine.Object.Destroy(shot); }
                }

                if (!File.Exists(file))
                {
                    Core.Warn("icon render produced no file for " + model.name);
                    return;
                }

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                if (tex.LoadImage(File.ReadAllBytes(file)))
                {
                    d.iconTexture = tex;

                    // The preview camera leaves a wide transparent margin, so at the same
                    // box size our icons read much smaller than the vanilla art, which
                    // fills its texture. Cropping to the visible pixels fixes that without
                    // having to inflate the box.
                    Rect crop = OpaqueBounds(tex);
                    d.icon = Sprite.Create(tex, crop, new Vector2(0.5f, 0.5f), 100f);

                    Core.Log("icon ready for " + model.name + " (" + tex.width + "x" + tex.height +
                             " cropped to " + (int)crop.width + "x" + (int)crop.height + ")");
                }
                else
                {
                    Core.Warn("icon PNG failed to decode: " + file);
                }
            }
            catch (Exception e)
            {
                Core.Warn("icon generation failed: " + e.Message);
            }
        }

        /// <summary>The on-screen prompt while the item is equipped. Null here means the
        /// player sees no "use item" hint at all.</summary>
        /// <summary>Smallest square rect containing every pixel that is not transparent.</summary>
        private static Rect OpaqueBounds(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a < 8) continue;   // ignore near-invisible edge pixels
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY) return new Rect(0f, 0f, w, h);   // nothing drawn

            const int pad = 4;
            minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(w - 1, maxX + pad); maxY = Mathf.Min(h - 1, maxY + pad);

            // Square, so the fixed aspect of the inventory slot cannot squash it.
            float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
            float cx = (minX + maxX) * 0.5f;
            float cy = (minY + maxY) * 0.5f;

            float x0 = Mathf.Clamp(cx - side * 0.5f, 0f, w - 1f);
            float y0 = Mathf.Clamp(cy - side * 0.5f, 0f, h - 1f);
            side = Mathf.Min(side, Mathf.Min(w - x0, h - y0));

            return new Rect(x0, y0, side, side);
        }

        private static InputHelp MakeInputHelp(bool aiming)
        {
            // GameUIController.SetBoardInputHelp() walks inputHelp.controller BEFORE
            // inputHelp.keyboard, with no null check. The InputHelp(keyboard) constructor
            // leaves controller null, which throws inside Item.Setup() and leaves the item
            // stuck in its Setup state - equipped, visible, and impossible to use.
            // Both arrays must be non-null.
            InputHelp help = new InputHelp();
            help.seperateControllerActions = false;
            help.controller = new InputDetails[0];

            // InputDetails.description is a LOCALISATION KEY, not display text:
            // UIInputPanel does LocalizationManager.GetTranslation(description) with no
            // fallback, so an unknown key renders as an empty label. These two keys are
            // ones the game itself passes in, so they are guaranteed to resolve.
            help.keyboard = aiming
                ? new InputDetails[]
                  {
                      new InputDetails(InputActions.UseBoardItem, "Use Item", false,
                          Pole.Positive, ControllerType.Keyboard, InputDetailsPriority.Large),
                      new InputDetails(InputActions.CancelBoardItem, "Back", false,
                          Pole.Positive, ControllerType.Keyboard, InputDetailsPriority.Normal),
                  }
                : new InputDetails[0];

            return help;
        }

        // ------------------------------------------------------------------- register

        internal static void RegisterAll()
        {
            foreach (CustomItemDef def in s_defs)
            {
                try { RegisterOne(def); }
                catch (Exception e) { Core.Warn("register failed for " + def.Name + ": " + e); }
            }
        }

        private static void RegisterOne(CustomItemDef def)
        {
            if (def.Id > MAX_ID)
            {
                Core.Warn("REFUSING '" + def.Name + "': itemID " + def.Id +
                          " does not fit in a byte and would hang the board on use.");
                return;
            }

            ItemDetails d = GetOrCreateDetails(def);

            // AddItem drops any entry sharing this itemID - including a vanilla one.
            ItemDetails clash = FindByID(def.Id);
            if (clash == d) return;   // already in the list; re-adding just spams the log

            if (clash != null)
            {
                Core.Warn("REFUSING '" + def.Name + "': itemID " + def.Id +
                          " is taken by '" + clash.itemNameToken + "' and would replace it.");
                return;
            }

            // GameManager.AddItem ends with an unguarded OnAddItem() call. When we register
            // before the ruleset groups exist - which is exactly what puts our items on the
            // pre-game rules screen - nothing has subscribed yet and it throws, after the
            // item is already in the list. Giving the event an empty subscriber keeps the
            // early registration silent instead of "working by accident".
            if (GameManager.OnAddItem == null)
                GameManager.OnAddItem = delegate { };

            GameManager.AddItem(d);
            Core.Log("registered '" + def.Name + "' id=" + def.Id +
                     (def.WeaponSpace ? " [weapon space]" : ""));
        }

        private static ItemDetails FindByID(ulong id)
        {
            ItemList list = GameManager.ItemList;
            if (list == null || list.items == null) return null;
            for (int i = 0; i < list.items.Length; i++)
            {
                if (list.items[i] != null && list.items[i].itemID == id) return list.items[i];
            }
            return null;
        }
    }

    /// <summary>
    /// GameManagerObj assigns GameManager.ItemList and then immediately calls
    /// SetupRulesets(). Registering here - rather than only at board start - is what puts
    /// our items into the pre-game rules screen, which builds its list from ItemList.items.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "SetupRulesets")]
    internal static class Patch_SetupRulesets
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            try { ItemRegistry.RegisterAll(); }
            catch (Exception e) { Core.Warn("early RegisterAll failed: " + e); }
        }
    }

    // Runs before SetupNetPrefabs() scans prefabRoot.
    [HarmonyPatch(typeof(WorkshopController), "PrepareModsForIngame")]
    internal static class Patch_PrepareModsForIngame
    {
        private static void Prefix()
        {
            try { ItemRegistry.EnsurePrefabs(); }
            catch (Exception e) { Core.Warn("EnsurePrefabs failed: " + e); }
        }
    }

    // Runs before enabledItems is built, so our items get an itemIndex like any other.
    [HarmonyPatch(typeof(ItemList), "Setup")]
    internal static class Patch_ItemList_Setup_Register
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            ItemRegistry.RegisterAll();
        }
    }
}
