using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ZP.Net;

namespace PummelCustomItems
{
    /// <summary>Fifty-fifty: it kills everyone else, or it kills you.</summary>
    public class DeathWandItem : InstantItem
    {
        protected override float FinishDelay { get { return 2.2f; } }

        protected override void Perform()
        {
            BoardPlayer me = player.BoardObject;
            bool backfires = rand.Next(0, 2) == 0;

            ModAssets.Play("snd_chaos", 1f);

            if (backfires)
            {
                Say(me, "Не тебе решать");
                Core.Log("DeathWand: backfired on player " + player.GlobalID);
                StartCoroutine(StrikeAfter(0.9f, new List<BoardPlayer> { me }));
            }
            else
            {
                // "Everyone but me" still respects the team rule: with friendly fire off,
                // teammates are not part of everyone.
                List<BoardPlayer> victims = OtherLivingPlayers();
                victims.RemoveAll(v => FriendlyFire.Spares(v, player));
                Say(me, "Все, кроме меня");
                Core.Log("DeathWand: killing " + victims.Count + " opponent(s)");
                StartCoroutine(StrikeAfter(0.9f, victims));
            }
        }

        private IEnumerator StrikeAfter(float delay, List<BoardPlayer> victims)
        {
            yield return new WaitForSeconds(delay);

            for (int i = 0; i < victims.Count; i++)
            {
                BoardPlayer v = victims[i];
                if (v == null || v.LocalHealth <= 0) continue;

                Effects.Blast(v.transform.position + Vector3.up, 2f);
                v.KillPlayer(player.BoardObject, v.transform.position + Vector3.up * 2f, 14f);
            }

            ModAssets.Play("snd_explode", 0.9f);
            try { GameManager.Board.boardCamera.AddShake(0.8f); } catch { }
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // A coin flip is worth taking when you are behind, not when you are winning.
            int ahead = 0;
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null || gp.BoardObject == user) continue;
                if (gp.BoardObject.Gold > user.Gold) ahead++;
            }
            return (ahead > 0) ? new ItemAIUse(user, 0.5f) : null;
        }
    }

    /// <summary>Meteors on everybody, thrower included - who is hurt, but left standing.</summary>
    public class ArmageddonItem : InstantItem
    {
        private const int DamageMin = 9;
        private const int DamageMax = 11;

        protected override float FinishDelay { get { return 3f; } }

        protected override void Perform()
        {
            ModAssets.Play("snd_doom", 1f);
            StartCoroutine(Rain());
        }

        private IEnumerator Rain()
        {
            List<BoardPlayer> targets = new List<BoardPlayer>();
            for (int i = 0; i < GameManager.PlayerCount; i++)
            {
                GamePlayer gp = GameManager.GetPlayerAt(i);
                if (gp == null || gp.BoardObject == null) continue;
                if (gp.BoardObject.LocalHealth <= 0) continue;

                // The user is always hit - that is the price of the item. Teammates follow the
                // team rule like every other item does.
                if (FriendlyFire.Spares(gp.BoardObject, player)) continue;
                targets.Add(gp.BoardObject);
            }

            // Every damage roll is drawn now, before the first meteor falls. Drawing each one
            // as its meteor dropped interleaved them with whatever else used the generator
            // while rocks were in the air - and when that happens depends on the frame rate,
            // which is different on every machine. Drawn up front, the sequence is the same
            // everywhere, so everybody takes the same damage on every screen.
            int[] damage = new int[targets.Count];
            for (int i = 0; i < damage.Length; i++)
                damage[i] = rand.Next(DamageMin, DamageMax + 1);

            Core.Log("Armageddon: " + targets.Count + " target(s)");

            // Staggered so it reads as a barrage rather than one simultaneous thud.
            for (int i = 0; i < targets.Count; i++)
            {
                BoardPlayer t = targets[i];
                int dmg = damage[i];
                Meteor.Drop(t.transform.position, () => Impact(t, dmg));
                yield return new WaitForSeconds(0.18f);
            }
        }

        private void Impact(BoardPlayer target, int damage)
        {
            if (target == null || target.LocalHealth <= 0) return;

            bool self = target == player.BoardObject;
            if (self)
            {
                int rolled = damage;
                damage = SelfDamage(target, rolled);
                Core.Log("Armageddon: the user takes " + damage + " of " + rolled +
                         " at " + target.LocalHealth + " hp");
            }

            if (damage > 0)
            {
                DamageInstance d = new DamageInstance
                {
                    damage = damage,
                    origin = target.transform.position + Vector3.up * 3f,
                    blood = true,
                    ragdoll = !self,   // the user stays on their feet - see SelfDamage
                    ragdollVel = 13f,
                    bloodVel = 16f,
                    bloodAmount = 1f,
                    details = "Armageddon",
                    killer = player.BoardObject,
                    removeKeys = true,
                };
                target.ApplyDamage(d);
            }

            Effects.Blast(target.transform.position, 2.6f);

            // Four identical bangs a fifth of a second apart read as a stutter; picking
            // between three makes the same volley sound like a barrage. Only the sound depends
            // on it, so this one is free to differ between machines - which is why it does not
            // touch the shared `rand`.
            string[] booms = { "snd_boom_a", "snd_boom_b", "snd_boom_c" };
            ModAssets.Play(booms[UnityEngine.Random.Range(0, booms.Length)], 0.8f);

            try { GameManager.Board.boardCamera.AddShake(0.45f); } catch { }
        }

        /// <summary>
        /// How much of their own meteor the user takes: what it rolled, but never their last
        /// point of health - which, while every hit kills, means nothing at all.
        ///
        /// The rain lands while the item is still in use, and when the item finishes the turn
        /// goes on to the user's roll. A user who is dead by then, or still sprawled in a
        /// ragdoll, can neither roll nor walk, and the turn has nowhere to go. Nothing else in
        /// Armageddon touches the player whose turn it is, which makes this the likeliest
        /// reading of the board freezing after it. So the user still bleeds and drops keys,
        /// but is not knocked down and comes out alive.
        /// </summary>
        private static int SelfDamage(BoardPlayer user, int rolled)
        {
            if (TempModifiers.EveryHitKills()) return 0;
            return Mathf.Clamp(rolled, 0, (int)user.LocalHealth - 1);
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // It hurts the user too, so only sensible when reasonably healthy and behind.
            if (user.LocalHealth <= 12) return null;
            return new ItemAIUse(user, 0.55f);
        }
    }

    /// <summary>
    /// A rock falling out of the sky onto a spot, with a callback on landing.
    ///
    /// The plain version - a sphere sliding down with a stretched shell behind it - had two
    /// problems: it arrived with no warning, so the eye never reached the impact in time, and
    /// a rigid trail moves like a stick rather than a flame. So the ground is marked first,
    /// and the trail is laid down as separate puffs that stay behind and fade, which is what
    /// makes a streak look burned through the air instead of carried along.
    /// </summary>
    internal class Meteor : MonoBehaviour
    {
        private const float FallHeight = 26f;
        private const float Speed = 34f;
        private const float PuffInterval = 0.022f;

        private Vector3 m_target;
        private Action m_onImpact;
        private Transform m_rock;
        private Transform m_mark;
        private Material m_markMat;
        private float m_puffTimer;
        private float m_spin;

        internal static void Drop(Vector3 target, Action onImpact)
        {
            GameObject host = new GameObject("PCI_Meteor");
            Meteor m = host.AddComponent<Meteor>();
            m.Setup(target, onImpact);
        }

        private void Setup(Vector3 target, Action onImpact)
        {
            m_target = target;
            m_onImpact = onImpact;
            m_spin = UnityEngine.Random.Range(140f, 320f);

            transform.position = target + Vector3.up * FallHeight;

            BuildRock();
            BuildGroundMark();
        }

        private void BuildRock()
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(transform, false);
            rock.transform.localScale = Vector3.one * 1.05f;
            rock.transform.localRotation = UnityEngine.Random.rotation;

            Collider c = rock.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Material mat = OwnedAssets.Own(gameObject, new Material(Shader.Find("Standard")));
            mat.color = new Color(0.24f, 0.16f, 0.14f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.08f));
            rock.GetComponent<Renderer>().material = mat;
            m_rock = rock.transform;

            GameObject lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);

            Light glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.18f);
            glow.range = 9f;
            glow.intensity = 4f;
            glow.shadows = LightShadows.None;
        }

        /// <summary>
        /// Marks the spot before the rock gets there. Without it the first anybody knows of a
        /// meteor is the explosion, which is far too late to be worth watching.
        /// </summary>
        private void BuildGroundMark()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Mark";

            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);

            go.transform.position = m_target + Vector3.up * 0.07f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 3.4f;

            Shader sh = Effects.UnlitShader();
            m_markMat = OwnedAssets.Own(go, new Material(sh != null ? sh : Shader.Find("Standard")));
            m_markMat.color = new Color(1f, 0.25f, 0.1f, 0.5f);

            Renderer r = go.GetComponent<Renderer>();
            r.material = m_markMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            m_mark = go.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, m_target, Speed * dt);
            if (m_rock != null) m_rock.Rotate(Vector3.one, m_spin * dt, Space.Self);

            // Tightens and brightens as the rock closes in, so the countdown is readable.
            if (m_mark != null)
            {
                float height = Mathf.Max(0f, transform.position.y - m_target.y);
                float closeness = 1f - Mathf.Clamp01(height / FallHeight);

                m_mark.localScale = Vector3.one * Mathf.Lerp(3.4f, 1.5f, closeness);

                Color mc = m_markMat.color;
                mc.a = 0.25f + 0.45f * closeness;
                m_markMat.color = mc;
            }

            m_puffTimer -= dt;
            if (m_puffTimer <= 0f)
            {
                m_puffTimer = PuffInterval;
                TrailPuff.Spawn(transform.position, UnityEngine.Random.Range(0.55f, 1f));
            }

            if ((transform.position - m_target).sqrMagnitude < 0.05f)
                Land();
        }

        /// <summary>
        /// Lands the rock - once. The callback used to run before the rock was cleared away, so
        /// a callback that threw left it where it was, to land again on the next frame and the
        /// one after, running the whole impact every time.
        /// </summary>
        private void Land()
        {
            Action onImpact = m_onImpact;
            m_onImpact = null;

            if (m_mark != null) Destroy(m_mark.gameObject);
            Destroy(gameObject);

            if (onImpact == null) return;
            try { onImpact(); }
            catch (Exception e) { Core.Warn("Meteor: impact failed: " + e); }
        }
    }

    /// <summary>One dot of the burning trail: dropped in place, then swells, cools and dies.</summary>
    internal class TrailPuff : MonoBehaviour
    {
        private const float Duration = 0.45f;

        // One material for every puff there will ever be; each puff's colour rides on a
        // property block instead. A material per puff meant ~45 new ones a second per meteor.
        private static Material s_shared;
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        private Renderer m_renderer;
        private MaterialPropertyBlock m_block;
        private float m_t;
        private float m_size;

        internal static void Spawn(Vector3 position, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PCI_TrailPuff";
            go.transform.position = position;

            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);

            TrailPuff p = go.AddComponent<TrailPuff>();
            p.m_size = size;

            if (s_shared == null)
            {
                Shader sh = Effects.UnlitShader();
                s_shared = new Material(sh != null ? sh : Shader.Find("Standard"));
                s_shared.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = s_shared;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            p.m_renderer = r;
            p.m_block = new MaterialPropertyBlock();
            p.SetColor(new Color(1f, 0.65f, 0.2f, 0.9f));
        }

        private void SetColor(Color c)
        {
            m_block.SetColor(ColorID, c);
            m_renderer.SetPropertyBlock(m_block);
        }

        private void Update()
        {
            m_t += Time.deltaTime / Duration;
            if (m_t >= 1f) { Destroy(gameObject); return; }

            // Swells and cools as it is left behind - burning air, not a bead on a string.
            transform.localScale = Vector3.one * m_size * (1f + m_t * 0.9f);

            SetColor(new Color(1f,
                               Mathf.Lerp(0.65f, 0.22f, m_t),
                               Mathf.Lerp(0.2f, 0.18f, m_t),
                               0.9f * (1f - m_t) * (1f - m_t)));
        }
    }

    /// <summary>
    /// Sucks in loose keys - including your own, which normally cannot be picked up - but
    /// only those inside an aimed wedge. Range and shape follow the magnet, which reaches
    /// 10 units in the direction the player points.
    /// </summary>
    public class VacuumItem : AimedItem
    {
        private const float Reach = 7.5f;
        private const float HalfAngle = 31.5f;   // 63-degree wedge in front of the player

        protected override float IndicatorReach { get { return Reach; } }
        protected override float IndicatorHalfAngle { get { return HalfAngle; } }

        protected override void PerformAimed(Vector3 dir)
        {
            BoardPlayer me = player.BoardObject;
            Vector3 origin = me.transform.position;

            BoardKey[] keys = UnityEngine.Object.FindObjectsOfType<BoardKey>();
            int taken = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                BoardKey k = keys[i];
                if (k == null) continue;
                // Already flying to somebody - leave it alone.
                if (k.CurState == BoardKey.BoardKeyState.Pickup) continue;

                Vector3 delta = k.transform.position - origin;
                delta.y = 0f;
                if (delta.magnitude > Reach) continue;
                if (Vector3.Angle(dir, delta) > HalfAngle) continue;

                // Counted everywhere, so every machine shows the same "+N"; picked up on the
                // host only, because PickupKey on the host already tells every other machine.
                taken++;
                if (!Authority) continue;

                try
                {
                    GameManager.KeyController.PickupKey(me, k.ID);
                }
                catch (Exception e)
                {
                    Core.Warn("Vacuum: key " + k.ID + " failed: " + e.Message);
                }
            }

            Say(me, taken > 0 ? ("+" + taken) : "Пусто");
            ModAssets.Play("snd_shuffle", 0.8f);
            Core.Log("Vacuum: pulled in " + taken + " of " + keys.Length + " key(s) in range");
        }

        protected static void Say(BoardPlayer who, string text)
        {
            DiceOverride.Announce(who, text);
        }

        /// <summary>
        /// Bots point it at the keys. A bot picks the vacuum because keys lie close by, and the
        /// usual aim - the nearest opponent - then swept the air in front of whoever stood
        /// nearest, nearly always for nothing. The aim is the key whose wedge takes in the most
        /// of the others.
        /// </summary>
        protected override Vector3 AiAimDirection()
        {
            Vector3 origin = player.BoardObject.transform.position;

            List<Vector3> dirs = new List<Vector3>();
            BoardKey[] keys = UnityEngine.Object.FindObjectsOfType<BoardKey>();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null || keys[i].CurState == BoardKey.BoardKeyState.Pickup) continue;
                Vector3 d = keys[i].transform.position - origin;
                d.y = 0f;
                if (d.magnitude > Reach || d.sqrMagnitude < 0.0001f) continue;
                dirs.Add(d.normalized);
            }

            Vector3 best = player.BoardObject.transform.forward;
            int bestCount = 0;
            for (int i = 0; i < dirs.Count; i++)
            {
                int n = 0;
                for (int j = 0; j < dirs.Count; j++)
                    if (Vector3.Angle(dirs[i], dirs[j]) <= HalfAngle) n++;
                if (n > bestCount) { bestCount = n; best = dirs[i]; }
            }
            return best;
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            // Only bother when there is a worthwhile pile somewhere nearby.
            BoardKey[] keys = UnityEngine.Object.FindObjectsOfType<BoardKey>();
            int near = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null) continue;
                if (Vector3.Distance(keys[i].transform.position, user.transform.position) <= Reach) near++;
            }
            if (near < 3) return null;
            return new ItemAIUse(user, Mathf.Clamp01(near / 10f));
        }
    }

    /// <summary>
    /// Until your next turn, keys knocked out of anybody go to you.
    ///
    /// KeyController.SpawnKeys already takes a "target" - the player the keys fly to - so
    /// the piggy bank only has to fill that in.
    /// </summary>
    public class PiggyBankItem : InstantItem
    {
        protected override void Perform()
        {
            PiggyBank.Arm(player.BoardObject, player.GlobalID);
            Say(player.BoardObject, "Копилка открыта");
            ModAssets.Play("snd_dice_charge", 0.9f);
            Core.Log("PiggyBank: armed for player " + player.GlobalID);
        }

        public override ItemAIUse GetTarget(BoardPlayer user)
        {
            return new ItemAIUse(user, 0.45f);
        }
    }

    internal static class PiggyBank
    {
        private static BoardPlayer s_collector;
        private static short s_ownerID = -1;

        // Survives the owner's first turn start, so the effect covers their own next turn
        // too rather than ending the moment it becomes useful to them.
        private static int s_turnsLeft;

        internal static BoardPlayer Collector { get { return s_collector; } }

        internal static void Arm(BoardPlayer collector, short ownerID)
        {
            s_collector = collector;
            s_ownerID = ownerID;
            s_turnsLeft = 2;
        }

        internal static void Reset()
        {
            s_collector = null;
            s_ownerID = -1;
            s_turnsLeft = 0;
        }

        internal static void OnTurnStarted(short playerID, BoardPlayer who)
        {
            if (s_ownerID != playerID || s_collector == null) return;

            s_turnsLeft--;
            if (s_turnsLeft > 0) return;

            DiceOverride.Announce(who, "Копилка закрыта");
            Core.Log("PiggyBank: expired for player " + playerID);
            s_collector = null;
            s_ownerID = -1;
        }
    }

    /// <summary>Redirects spilled keys to the piggy bank's owner while it is open.</summary>
    [HarmonyPatch(typeof(KeyController), "SpawnKeys",
                  new[] { typeof(int), typeof(BoardPlayer), typeof(BoardPlayer) })]
    internal static class Patch_SpawnKeys
    {
        private static void Prefix(int count, BoardPlayer owner, ref BoardPlayer target)
        {
            BoardPlayer collector = PiggyBank.Collector;
            if (collector == null || target != null) return;
            if (collector == owner) return;      // your own spill is not a windfall

            target = collector;
            Core.Log("PiggyBank: " + count + " key(s) redirected to " + collector.GamePlayer.Name);
        }
    }
}
