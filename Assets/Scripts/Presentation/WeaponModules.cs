using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class WeaponModules
    {
        private readonly Transform _mount;
        private readonly Material _metal;
        private readonly MaterialPropertyBlock _finish = new();
        private WeaponInstance _instance;
        private GameObject _assembly;

        internal WeaponModules(Transform mount, Material metal)
        {
            _mount = mount;
            _metal = metal;
        }

        internal void Equip(WeaponInstance instance)
        {
            if (_instance == instance)
            {
                return;
            }

            _instance = instance;
            if (_assembly != null)
            {
                _assembly.SetActive(false);
                Object.Destroy(_assembly);
            }

            if (instance == null || (instance.Rarity == WeaponRarity.Common && instance.Affixes.Count == 0))
            {
                return;
            }

            _assembly = new GameObject("Field Modifications");
            _assembly.transform.SetParent(_mount, false);
            var pistol = instance.Definition.Id == WeaponId.Directive;
            var width = pistol ? 0.06f : instance.Definition.Id == WeaponId.Compliance ? 0.094f : 0.12f;
            var finish = instance.Rarity switch
            {
                WeaponRarity.Uncommon => new Color(0.39f, 0.70f, 0.63f),
                WeaponRarity.Rare => new Color(0.34f, 0.57f, 0.75f),
                WeaponRarity.Epic => new Color(0.65f, 0.50f, 0.78f),
                _ => new Color(0.60f, 0.63f, 0.63f)
            };

            for (var index = 0; index < (int)instance.Rarity; index++)
            {
                Add("ServiceSocket", new Vector3(width, 0.01f, index * 0.052f), new Vector3(0.046f, 0.046f, 0.044f), finish);
            }

            for (var index = 0; index < instance.Affixes.Count; index++)
            {
                var modifier = instance.Affixes[index].Kind;
                var color = modifier switch
                {
                    WeaponModifier.ExplosiveCore or WeaponModifier.OverloadCell => new Color(0.88f, 0.38f, 0.15f),
                    WeaponModifier.CryoPayload => new Color(0.22f, 0.59f, 0.66f),
                    WeaponModifier.RicochetChamber or WeaponModifier.GyroscopicStabilizer => new Color(0.34f, 0.38f, 0.42f),
                    _ => new Color(0.65f, 0.67f, 0.64f)
                };
                var side = index == 1 ? 1f : -1f;
                var depth = index == 2 ? 0.21f : 0.075f;
                var position = new Vector3(side * width, index == 1 ? -0.043f : 0.008f, depth);
                Add(modifier.ToString(), position, new Vector3(0.079f, 0.083f, 0.125f), color);
            }
        }

        internal void SetVisible(bool visible)
        {
            if (_assembly != null && _assembly.activeSelf != visible)
            {
                _assembly.SetActive(visible);
            }
        }

        private void Add(string name, Vector3 position, Vector3 scale, Color color)
        {
            var asset = Resources.Load<GameObject>("ARC Modules/" + name);
            if (asset == null)
            {
                return;
            }

            var part = Object.Instantiate(asset, _assembly.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.gameObject.layer = _mount.gameObject.layer;
                renderer.sharedMaterial = _metal;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                _finish.SetColor("_BaseColor", color);
                _finish.SetColor("_EmissionColor", color * 0.24f);
                renderer.SetPropertyBlock(_finish);
            }
        }
    }
}
