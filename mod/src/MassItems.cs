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
                List<BoardPlayer> victims = OtherLivingPlayers();
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

    /// <summary>Meteors on everybody, thrower included.</summary>
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
                targets.Add(gp.BoardObject);   // nobody is spared, the user least of all
            }

            Core.Log("Armageddon: " + targets.Count + " target(s)");

            // Staggered so it reads as a barrage rather than one simultaneous thud.
            for (int i = 0; i < targets.Count; i++)
            {
                BoardPlayer t = targets[i];
                int damage = rand.Next(DamageMin, DamageMax + 1);
                Meteor.Drop(t.transform.position, () => Impact(t, damage));
                yield return new WaitForSeconds(0.18f);
            }
        }

        private void Impact(BoardPlayer target, int damage)
        {
            if (target == null || target.LocalHealth <= 0) return;

            DamageInstance d = new DamageInstance
            {
                damage = damage,
                origin = target.transform.position + Vector3.up * 3f,
                blood = true,
                ragdoll = true,
                ragdollVel = 13f,
                bloodVel = 16f,
                bloodAmount = 1f,
                details = "Armageddon",
                killer = player.BoardObject,
                removeKeys = true,
            };
            target.ApplyDamage(d);

            Effects.Blast(target.transform.position, 2.6f);

            // Four identical bangs a fifth of a second apart read as a stutter; picking
            // between three makes the same volley sound like a barrage.
            string[] booms = { "snd_boom_a", "snd_boom_b", "snd_boom_c" };
            ModAssets.Play(booms[rand.Next(booms.Length)], 0.8f);

            try { GameManager.Board.boardCamera.AddShake(0.45f); } catch { }
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

            Material mat = new Material(Shader.Find("Standard"));
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
            m_markMat = new Material(sh != null ? sh : Shader.Find("Standard"));
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
            {
                if (m_mark != null) Destroy(m_mark.gameObject);
                if (m_onImpact != null) m_onImpact();
                Destroy(gameObject);
            }
        }
    }

    /// <summary>One dot of the burning trail: dropped in place, then swells, cools and dies.</summary>
    internal class TrailPuff : MonoBehaviour
    {
        private const float Duration = 0.45f;

        private Material m_mat;
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

            Shader sh = Effects.UnlitShader();
            p.m_mat = new Material(sh != null ? sh : Shader.Find("Standard"));
            p.m_mat.color = new Color(1f, 0.65f, 0.2f, 0.9f);

            Renderer r = go.GetComponent<Renderer>();
            r.material = p.m_mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void Update()
        {
            m_t += Time.deltaTime / Duration;
            if (m_t >= 1f) { Destroy(gameObject); return; }

            // Swells and cools as it is left behind - burning air, not a bead on a string.
            transform.localScale = Vector3.one * m_size * (1f + m_t * 0.9f);

            Color c = m_mat.color;
            c.g = Mathf.Lerp(0.65f, 0.22f, m_t);
            c.b = Mathf.Lerp(0.2f, 0.18f, m_t);
            c.a = 0.9f * (1f - m_t) * (1f - m_t);
            m_mat.color = c;
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

                try
                {
                    GameManager.KeyController.PickupKey(me, k.ID);
                    taken++;
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
