using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class RoutePassage : MonoBehaviour
    {
        private const float Width = 4.8f;
        private const float Height = 3.6f;
        private const float Wall = 0.36f;
        private const float ForkNear = 2f;
        private const float ForkFar = ForkNear + Width;
        private const float Center = (ForkNear + ForkFar) / 2f;
        private const float BranchCenter = 11f;
        private const float Destination = 16f;

        private readonly Transform[] _panels = new Transform[2];
        private readonly BoxCollider[] _barriers = new BoxCollider[2];
        private Material _shell;
        private Material _accent;
        private int _count;

        internal int Selected { get; private set; } = -1;

        internal void Build(RoomRuntime room, System.Collections.Generic.IReadOnlyList<RouteNode> options, Texture iconAtlas, CombatWeapon weapon)
        {
            _count = options.Count;
            var gate = room.Encounter.Gate.transform;
            var module = gate.parent;
            for (var index = 0; index < module.childCount; index++)
            {
                var item = module.GetChild(index);
                if (item != gate)
                {
                    item.gameObject.SetActive(false);
                }
            }

            _shell = gate.Find("Sliding Panel").GetComponent<MeshRenderer>().sharedMaterial;
            _accent = gate.Find("Sliding Panel/Interlock Light").GetComponent<MeshRenderer>().sharedMaterial;
            FloorAndCeiling(0f, 0f, Width, ForkFar);
            HorizontalWall(0f, ForkFar + Wall * 0.5f, Width + Wall * 2f);

            for (var index = 0; index < 2; index++)
            {
                var sign = index == 0 ? -1f : 1f;
                SideWall(sign * Width * 0.5f, 0f, ForkNear, sign);
                if (index >= _count)
                {
                    SideWall(sign * Width * 0.5f, ForkNear, ForkFar, sign);
                    continue;
                }

                BuildBranch(index, sign, options[index], iconAtlas, weapon);
            }
        }

        internal void Open(bool reducedMotion)
        {
            for (var index = 0; index < _count; index++)
            {
                OpenPanel(index, reducedMotion);
            }
        }

        internal int Crossing(Vector3 position)
        {
            var point = transform.InverseTransformPoint(position);
            if (Selected >= 0 || point.z <= ForkNear || point.z >= ForkFar || point.y < 0f || point.y > Height || Mathf.Abs(point.x) < Width * 0.5f + 0.65f)
            {
                return -1;
            }

            var branch = point.x < 0f ? 0 : 1;
            return branch < _count && !_barriers[branch].enabled ? branch : -1;
        }

        internal bool Select(int branch, bool reducedMotion)
        {
            if (Selected >= 0 || branch < 0 || branch >= _count || _barriers[branch].enabled)
            {
                return false;
            }

            Selected = branch;
            for (var index = 0; index < _count; index++)
            {
                if (index == branch)
                {
                    continue;
                }

                _barriers[index].enabled = true;
                var panel = _panels[index];
                panel.DOKill();
                if (reducedMotion)
                {
                    panel.localPosition = new Vector3(panel.localPosition.x, Height * 0.5f, Center);
                }
                else
                {
                    panel.DOLocalMoveY(Height * 0.5f, 0.25f).SetUpdate(true).SetLink(gameObject);
                }
            }

            return true;
        }

        internal Pose DestinationSocket(int branch)
        {
            var x = branch == 0 ? -BranchCenter : BranchCenter;
            return new Pose(transform.TransformPoint(new Vector3(x, 0f, Destination)), transform.rotation);
        }

        internal Vector3 CrossingPoint(int branch)
        {
            return transform.TransformPoint(new Vector3((branch == 0 ? -1f : 1f) * 3.35f, 0.9f, Center));
        }

        internal Vector3 TurnPoint(int branch)
        {
            var x = branch == 0 ? -BranchCenter : BranchCenter;
            return transform.TransformPoint(new Vector3(x, 0.9f, Center));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (var branch = 0; branch < _count; branch++)
            {
                var door = CrossingPoint(branch);
                var turn = TurnPoint(branch);
                Gizmos.DrawWireSphere(door, 0.30f);
                Gizmos.DrawLine(door, turn);
                Gizmos.DrawLine(turn, DestinationSocket(branch).position);
            }
        }
#endif

        private void OpenPanel(int index, bool reducedMotion)
        {
            _barriers[index].enabled = false;
            var panel = _panels[index];
            if (reducedMotion)
            {
                panel.localPosition += Vector3.up * Height;
                return;
            }

            panel.DOLocalMoveY(Height * 1.5f, 0.28f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }

        private void BuildBranch(int index, float sign, RouteNode destination, Texture atlas, CombatWeapon weapon)
        {
            var inner = BranchCenter - Width * 0.5f;
            var outer = BranchCenter + Width * 0.5f;
            var edge = Width * 0.5f;
            FloorAndCeiling(sign * (edge + outer) * 0.5f, ForkNear, outer - edge, ForkFar);
            FloorAndCeiling(sign * BranchCenter, ForkFar, Width, Destination);
            HorizontalWall(sign * (edge + outer) * 0.5f, ForkNear - Wall * 0.5f, outer - edge);
            HorizontalWall(sign * (edge + inner) * 0.5f, ForkFar + Wall * 0.5f, inner - edge);
            SideWall(sign * outer, ForkNear - Wall, Destination, sign);
            SideWall(sign * inner, ForkFar, Destination, -sign);
            var center = new Vector3(sign * edge, Height * 0.5f, Center);
            var dimensions = new Vector3(0.28f, Height, Width);
            _panels[index] = Block("Route Door Panel", center, dimensions, _shell, false);
            _barriers[index] = gameObject.AddComponent<BoxCollider>();
            _barriers[index].center = center;
            _barriers[index].size = dimensions;
            Block("Route Door Light", new Vector3(sign * (edge - 0.16f), Height - 0.12f, Center), new Vector3(0.045f, 0.10f, Width - 0.12f), _accent, false);
            Sign(sign, destination, atlas, weapon);
        }

        private void Sign(float sign, RouteNode destination, Texture atlas, CombatWeapon weapon)
        {
            var reward = destination.Reward;
            var root = new GameObject("Reward Sign", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.localPosition = new Vector3(sign * (Width * 0.5f - 0.2f), 2.8f, Center);
            rect.localRotation = Quaternion.Euler(0f, sign * 90f, 0f);
            rect.localScale = Vector3.one * 0.006f;
            rect.sizeDelta = new Vector2(520f, 108f);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var textRoot = new GameObject("Reward", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = (RectTransform)textRoot.transform;
            textRect.SetParent(rect, false);
            textRect.anchoredPosition = new Vector2(54f, 0f);
            textRect.sizeDelta = new Vector2(405f, 96f);
            var label = textRoot.GetComponent<TextMeshProUGUI>();
            label.text = destination.Room.Category == RoomCategory.Supervisor ? "SUPERVISOR CHAMBER\nCENTRAL SECURITY" : RouteCard.RewardText(reward, weapon).Replace("   ", "\n");
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 29f;
            label.color = new Color(0.75f, 0.98f, 1f);
            label.raycastTarget = false;
            if (atlas == null || reward == RoomReward.None)
            {
                return;
            }

            var iconRoot = new GameObject("Reward Icon", typeof(RectTransform), typeof(RawImage));
            var iconRect = (RectTransform)iconRoot.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchoredPosition = new Vector2(-207f, 0f);
            iconRect.sizeDelta = Vector2.one * 82f;
            var icon = iconRoot.GetComponent<RawImage>();
            var frame = (int)reward - 1;
            icon.texture = atlas;
            icon.uvRect = new Rect((frame & 1) * 0.5f, frame < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            icon.raycastTarget = false;
        }

        private void FloorAndCeiling(float x, float start, float width, float end)
        {
            var dimensions = new Vector3(width, Wall, end - start);
            var z = (start + end) * 0.5f;
            Block("Transfer Deck", new Vector3(x, -Wall * 0.5f, z), dimensions, _shell);
            Block("Transfer Ceiling", new Vector3(x, Height + Wall * 0.5f, z), dimensions, _shell);
        }

        private void SideWall(float x, float start, float end, float sign)
        {
            Block("Transfer Bulkhead", new Vector3(x + sign * Wall * 0.5f, Height * 0.5f, (start + end) * 0.5f), new Vector3(Wall, Height, end - start), _shell);
        }

        private void HorizontalWall(float x, float z, float width)
        {
            Block("Transfer Bulkhead", new Vector3(x, Height * 0.5f, z), new Vector3(width, Height, Wall), _shell);
        }

        private Transform Block(string title, Vector3 center, Vector3 scale, Material material, bool solid = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = title;
            block.transform.SetParent(transform, false);
            block.transform.localPosition = center;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.GetComponent<Collider>().enabled = solid;
            return block.transform;
        }
    }
}
