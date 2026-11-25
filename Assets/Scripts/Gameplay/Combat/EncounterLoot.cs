using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class EncounterLoot : MonoBehaviour
    {
        private sealed class AmmoDrop
        {
            internal int Id;
            internal WeaponId Type;
            internal int Amount;
            internal GameObject Model;
            internal Transform Icon;
            internal SecurityEnemy Enemy;
            internal bool WasDead;
        }

        private sealed class GunDrop
        {
            internal WeaponInstance Instance;
            internal GameObject Model;
        }

        private static readonly int s_color = Shader.PropertyToID("_BaseColor");

        private readonly List<AmmoDrop> _ammo = new(32);
        private readonly List<GunDrop> _guns = new(2);
        private RoomRuntime _room;
        private CombatWeapon _weapon;
        private Material _emitter;
        private Material _shell;
        private GameObject[] _pickupModels;
        private Sprite[] _ammoIcons;
        private MaterialPropertyBlock _colorBlock;
        private GunDrop _hover;
        private string _hint = string.Empty;

        internal string Hint => _hint;

        internal void Prepare(RoomRuntime room, RouteNode node, int seed, CombatWeapon weapon, float scavenger, GameObject[] models, Sprite[] ammoIcons, WeaponId cacheType)
        {
            _room = room;
            _weapon = weapon;
            _pickupModels = models;
            _ammoIcons = ammoIcons;
            _emitter = Resources.Load<Material>("ARC-01 Bolt");
            _shell = room.Encounter.Gate.transform.Find("Sliding Panel").GetComponent<MeshRenderer>().sharedMaterial;
            _colorBlock = new MaterialPropertyBlock();

            var active = weapon.ActiveDefinition.Id;
            var random = new System.Random(unchecked(seed * 613 ^ node.Id * 7919 ^ node.Variant * 29 ^ 0x414D4D4F));
            var enemies = room.Encounter.Enemies;
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                var roll = random.NextDouble();
                var type = random.NextDouble() < 0.82d ? active : (WeaponId)random.Next(4);
                var heavy = enemy.Kind is SecurityEnemyKind.Auditor or SecurityEnemyKind.Enforcer;
                if (roll < (heavy ? 0.6f : 0.35f) + scavenger)
                {
                    var id = ((ICombatDamageable)enemy).RewardId;
                    _ammo.Add(new AmmoDrop
                    {
                        Id = id,
                        Type = type,
                        Amount = PackSize(type) * (heavy ? 2 : 1),
                        Enemy = enemy
                    });
                }
            }

            Physics.SyncTransforms();
            PlaceSupplyPacks(node.Id, active, random, room.AmmoSockets);

            if (node.Reward == RoomReward.AmmunitionCache)
            {
                AddAmmo(-node.Id * 100 - 20, cacheType, CacheSize(cacheType), RewardPosition(0f, 3.4f), "Ammunition Cache");
            }
        }

        private void PlaceSupplyPacks(int roomId, WeaponId active, System.Random random, Transform[] sockets)
        {
            Span<int> indices = stackalloc int[sockets.Length];
            for (var index = 0; index < indices.Length; index++)
            {
                indices[index] = index;
            }

            var count = Mathf.Min(5, indices.Length);
            for (var index = 0; index < count; index++)
            {
                var selected = random.Next(index, indices.Length);
                (indices[index], indices[selected]) = (indices[selected], indices[index]);
                var type = index < 4 ? active : (WeaponId)random.Next(4);
                var point = sockets[indices[index]].position;
                AddAmmo(-roomId * 100 - index - 1, type, PackSize(type), point, "Ammunition");
            }
        }

        internal static int CacheSize(WeaponId type)
        {
            return type switch
            {
                WeaponId.Termination => 21,
                WeaponId.Deadline => 9,
                _ => 45
            };
        }

        internal void DropWeapons(int roomId, WeaponInstance[] rewards, bool reduced)
        {
            for (var index = 0; index < rewards.Length; index++)
            {
                var position = RewardPosition(index == 0 ? -1.3f : 1.3f, 1.7f);
                AddGun(rewards[index], position, reduced);
                var type = rewards[index].Definition.Id;
                AddAmmo(-roomId * 100 - 40 - index, type, PackSize(type), RewardPosition(index == 0 ? -1.8f : 1.8f, 2.8f), "Ammunition");
            }
        }

        internal void Advance(PlaygroundPlayer player, bool collect, bool safe, bool interact)
        {
            var capsule = player.Capsule;
            var aim = player.AimRay;
            var up = player.AimUp;
            foreach (var drop in _ammo)
            {
                if (_weapon.Collected(drop.Id))
                {
                    if (drop.Model != null && drop.Model.activeSelf)
                    {
                        drop.Model.SetActive(false);
                    }

                    continue;
                }

                if (drop.Enemy != null)
                {
                    var dead = !drop.Enemy.Health.Alive;
                    if (dead != drop.WasDead)
                    {
                        SyncEnemyDrop(drop, dead);
                    }
                }

                var model = drop.Model;
                if (model == null || !model.activeSelf)
                {
                    continue;
                }

                Animate(model, player.ReducedMotion);
                if (drop.Icon != null)
                {
                    var direction = drop.Icon.position - aim.origin;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        drop.Icon.rotation = Quaternion.LookRotation(direction, up);
                    }
                }

                if (!collect)
                {
                    continue;
                }

                var position = model.transform.position;
                var contact = capsule.ClosestPoint(position);
                if ((position - contact).sqrMagnitude < 0.44f && !Physics.Linecast(contact, position, 1, QueryTriggerInteraction.Ignore) && _weapon.CollectAmmo(drop.Id, drop.Type, drop.Amount))
                {
                    model.SetActive(false);
                }
            }

            GunDrop hover = null;
            if (safe)
            {
                var closest = 3.25f;
                foreach (var drop in _guns)
                {
                    Animate(drop.Model, player.ReducedMotion);
                    var offset = drop.Model.transform.position - aim.origin;
                    var depth = Vector3.Dot(aim.direction, offset);
                    if (depth <= 0f || depth > closest || (offset - aim.direction * depth).sqrMagnitude > 0.34f || Physics.Raycast(aim.origin, offset.normalized, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    closest = depth;
                    hover = drop;
                }
            }

            if (hover != _hover)
            {
                _hover = hover;
                _hint = hover == null ? string.Empty : Describe(hover.Instance);
            }

            if (hover != null && interact)
            {
                var previous = _weapon.CollectWeapon(hover.Instance);
                var position = hover.Model.transform.position;
                hover.Model.SetActive(false);
                Destroy(hover.Model);
                if (previous == null)
                {
                    _guns.Remove(hover);
                }
                else
                {
                    hover.Instance = previous;
                    hover.Model = BuildGun(previous, position, player.ReducedMotion);
                }

                _hover = null;
                _hint = string.Empty;
                player.SealLoadout();
            }
        }

        private void AddGun(WeaponInstance instance, Vector3 position, bool reduced)
        {
            _guns.Add(new GunDrop
            {
                Instance = instance,
                Model = BuildGun(instance, position, reduced)
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugSpawnWeapon(WeaponInstance instance, Vector3 position, bool reduced)
        {
            AddGun(instance, OnSurface(position), reduced);
        }

        internal void DebugSpawnAmmo(int id, WeaponId type, int amount, Vector3 position)
        {
            AddAmmo(id, type, amount, OnSurface(position), "QA Ammunition");
        }
#endif

        internal void DropEmergencyAmmo(int roomId, WeaponId type, Vector3 position)
        {
            AddAmmo(-roomId * 100 - 30, type, PackSize(type) * 2, OnSurface(position), "Emergency Supply");
        }

        private void SyncEnemyDrop(AmmoDrop drop, bool dead)
        {
            drop.WasDead = dead;
            if (dead)
            {
                var position = OnSurface(drop.Enemy.Body.bounds.center == Vector3.zero ? drop.Enemy.transform.position : drop.Enemy.Body.bounds.center);
                if (drop.Model == null)
                {
                    drop.Model = BuildAmmo(drop.Type, position, "Enemy Ammunition", out drop.Icon);
                }

                drop.Model.transform.position = position;
            }

            if (drop.Model != null)
            {
                drop.Model.SetActive(dead);
            }
        }

        private void AddAmmo(int id, WeaponId type, int amount, Vector3 position, string label)
        {
            _ammo.Add(new AmmoDrop
            {
                Id = id,
                Type = type,
                Amount = amount,
                Model = BuildAmmo(type, position, label, out var icon),
                Icon = icon
            });
        }

        private string Describe(WeaponInstance instance)
        {
            var definition = instance.Definition;
            var text = $"<b>{definition.DisplayName}</b>   {instance.Rarity.ToString().ToUpperInvariant()}\n";
            foreach (var affix in instance.Affixes)
            {
                text += ObjectNames(affix.Kind) + "   ";
            }

            var tuning = definition.Tuning;
            var slug = instance.Modifier(WeaponModifier.SlugConversion);
            var pellets = slug > 0f ? slug : tuning.Pellets;
            var damage = _weapon.BaseDamage(definition) * pellets;
            var equippedDamage = _weapon.DamageFor(_weapon.ActiveSlot) * _weapon.PelletsFor(_weapon.ActiveSlot);
            var comparison = _weapon.HasSecondary ? $" · replaces {_weapon.ActiveName}" : " · fills slot 2";
            return text + $"\n{damage:0.#} DMG / volley ({equippedDamage:0.#} equipped)   {instance.Stored.Magazine} MAG   {AmmoName(definition.Id)}\n[G] PICK UP{comparison}";
        }

        private string ObjectNames(WeaponModifier modifier)
        {
            return modifier switch
            {
                WeaponModifier.BurstModule => "Burst Module",
                WeaponModifier.SlugConversion => "Slug Conversion",
                WeaponModifier.ExplosiveCore => "Explosive Core",
                WeaponModifier.RicochetChamber => "Ricochet Chamber",
                WeaponModifier.CryoPayload => "Cryo Payload",
                WeaponModifier.FanOutput => "Fan Output",
                WeaponModifier.GyroscopicStabilizer => "Gyroscopic Stabilizer",
                WeaponModifier.PhaseAccelerator => "Phase Accelerator",
                WeaponModifier.OverloadCell => "Overload Cell",
                _ => "Splitter"
            };
        }

        internal static string AmmoName(WeaponId type)
        {
            return type switch
            {
                WeaponId.Compliance => "CARBINE CELLS",
                WeaponId.Termination => "SCATTER SHELLS",
                WeaponId.Deadline => "RAIL CELLS",
                _ => "PISTOL CELLS"
            };
        }

        private int PackSize(WeaponId type)
        {
            return type switch
            {
                WeaponId.Compliance => 18,
                WeaponId.Termination => 7,
                WeaponId.Deadline => 3,
                _ => 12
            };
        }

        private Vector3 RewardPosition(float x, float distance)
        {
            var exit = _room.Exit;
            return OnSurface(exit.position + exit.right * x - exit.forward * distance);
        }

        private Vector3 OnSurface(Vector3 point)
        {
            var up = _room.transform.up;
            if (Physics.Raycast(point + up * 0.25f, -up, out var floor, 16f, 1, QueryTriggerInteraction.Ignore))
            {
                return floor.point + up * 0.52f;
            }

            return point;
        }

        private GameObject BuildAmmo(WeaponId type, Vector3 position, string label, out Transform iconTransform)
        {
            var root = Root(label + " / " + AmmoName(type), position);
            var color = type switch
            {
                WeaponId.Compliance => new Color(0.2f, 0.85f, 1.45f),
                WeaponId.Termination => new Color(1.55f, 0.48f, 0.16f),
                WeaponId.Deadline => new Color(0.69f, 0.38f, 1.45f),
                _ => new Color(1.35f, 1.09f, 0.23f)
            };

            Part(root, Vector3.zero, new Vector3(0.54f, 0.31f, 0.40f), _shell);
            var cells = type == WeaponId.Deadline ? 1 : type == WeaponId.Termination ? 2 : type == WeaponId.Compliance ? 3 : 4;
            for (var index = 0; index < cells; index++)
            {
                var x = (index - (cells - 1) * 0.5f) * 0.115f;
                Part(root, new Vector3(x, 0.12f, 0f), new Vector3(0.075f, 0.16f, 0.43f), _emitter, color);
            }

            Part(root, new Vector3(0f, -0.13f, 0f), new Vector3(0.57f, 0.035f, 0.42f), _emitter, color);
            iconTransform = null;
            var typeIndex = (int)type;
            if (_ammoIcons != null && typeIndex < _ammoIcons.Length && _ammoIcons[typeIndex] is { } sprite)
            {
                var icon = new GameObject("Ammo Type", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                icon.sprite = sprite;
                icon.color = color;
                iconTransform = icon.transform;
                iconTransform.SetParent(root, false);
                iconTransform.localPosition = Vector3.up * 0.34f;
                iconTransform.localScale = Vector3.one * (0.29f / sprite.bounds.size.x);
            }

            return root.gameObject;
        }

        private GameObject BuildGun(WeaponInstance instance, Vector3 position, bool reduced)
        {
            var root = Root(instance.Definition.DisplayName, position);
            var color = instance.Rarity switch
            {
                WeaponRarity.Epic => new Color(1.1f, 0.4f, 1.5f),
                WeaponRarity.Rare => new Color(0.32f, 0.72f, 1.55f),
                WeaponRarity.Uncommon => new Color(0.29f, 1.2f, 0.61f),
                _ => new Color(1.1f, 1.14f, 1.19f)
            };

            var type = instance.Definition.Type;
            var length = type == WeaponType.SemiAutomatic ? 0.64f : type == WeaponType.Charged ? 1.45f : 1.1f;
            var id = (int)instance.Definition.Id;
            if (_pickupModels != null && id < _pickupModels.Length && _pickupModels[id] != null)
            {
                Instantiate(_pickupModels[id], root, false);
            }

            Part(root, new Vector3(0f, -0.24f, 0f), new Vector3(0.29f, 0.026f, length * 0.87f), _emitter, color);

            if (!reduced)
            {
                root.DOScale(1f, 0.24f).From(0.05f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(root.gameObject);
            }

            return root.gameObject;
        }

        private Transform Root(string title, Vector3 position)
        {
            var model = new GameObject(title).transform;
            model.SetParent(transform, false);
            model.position = position;
            return model;
        }

        private void Part(Transform root, Vector3 position, Vector3 scale, Material material, Color? color = null)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (color.HasValue)
            {
                _colorBlock.SetColor(s_color, color.Value);
                renderer.SetPropertyBlock(_colorBlock);
            }
        }

        private void Animate(GameObject model, bool reducedMotion)
        {
            if (!reducedMotion)
            {
                model.transform.Rotate(0f, Time.unscaledDeltaTime * 30f, 0f, Space.Self);
            }
        }
    }
}
