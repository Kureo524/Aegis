using System.Collections.Generic;
using Features.Events;
using Features.Meta;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Features.UI.Panels {
    /// <summary>
    /// Meta-progression screen of the main menu (design doc §9.7, "Progression" button): the
    /// persistent currency and the champion's tree, nodes laid out by tier with lines from their
    /// prerequisites. A node shows its level and the cost of the next one; hover for what it does;
    /// click "Améliorer" to buy. Escape or "Retour" closes it. Own UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MetaPanel : MonoBehaviour {
        private const float NodeWidth = 190f, NodeHeight = 112f, ColumnGap = 64f, RowGap = 18f;

        private MetaProgress _progress;
        private readonly DisposableBag _subscriptions = new();
        private UiTooltip _tooltip;
        private VisualElement _overlay, _tree;
        private Label _currency, _title;
        private bool _open;

        public bool IsOpen => _open;

        [Inject]
        public void Construct(IEventBus eventBus, MetaProgress progress) {
            _progress = progress;
            _subscriptions.Add(eventBus.Subscribe<MetaTreeRequestedEvent>(_ => Show(true)));
            _subscriptions.Add(eventBus.Subscribe<UiCancelEvent>(_ => Show(false)));
            _subscriptions.Add(eventBus.Subscribe<MetaChangedEvent>(_ => Refresh()));
        }

        private void Start() {
            _tooltip = new UiTooltip();
            _overlay = MenuStyles.Overlay(out VisualElement box);
            box.style.width = StyleKeyword.Null;
            box.AddToClassList("ae-meta");

            var header = new VisualElement();
            header.AddToClassList("ae-shop__header");
            _title = MenuStyles.Title("Progression");
            _title.style.marginBottom = 0;
            _currency = new Label();
            _currency.AddToClassList("ae-gold");
            _currency.style.fontSize = 22;
            header.Add(_title);
            header.Add(_currency);
            box.Add(header);
            box.Add(MenuStyles.Hint("Gagne des éclats en terminant le stage (vagues bonus comprises) et améliore ton champion pour toutes les parties."));

            _tree = new VisualElement();
            _tree.AddToClassList("ae-meta__tree");
            box.Add(_tree);

            Button back = MenuStyles.Button("Retour", () => Show(false));
            back.style.width = 180;
            back.style.alignSelf = Align.FlexEnd;
            box.Add(back);

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            root.Add(_overlay);
            root.Add(_tooltip.Root);
            Show(_open);
        }

        private void OnDestroy() => _subscriptions.Dispose();

        public void Show(bool open) {
            _open = open;
            if (_overlay == null) return;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) Refresh();
            else _tooltip.Hide();
        }

        private void Refresh() {
            if (_tree == null || _progress?.Settings == null) return;
            MetaTreeDefinition tree = _progress.Settings.Trees.Length > 0 ? _progress.Settings.Trees[0] : null;
            _currency.text = $"{_progress.Currency} {_progress.Settings.CurrencyName}";
            _tree.Clear();
            if (tree == null) return;
            _title.text = $"Progression — {tree.DisplayName}";

            var centers = new Dictionary<MetaNodeDefinition, Rect>();
            float width = 0f, height = 0f;
            foreach (MetaNodeDefinition node in tree.Nodes) {
                if (node == null) continue;
                var rect = new Rect(node.Position.x * (NodeWidth + ColumnGap), node.Position.y * (NodeHeight + RowGap), NodeWidth, NodeHeight);
                centers[node] = rect;
                width = Mathf.Max(width, rect.xMax);
                height = Mathf.Max(height, rect.yMax);
            }
            _tree.style.width = width;
            _tree.style.height = height;

            var links = new LinkLayer(tree, centers, _progress);
            UiStyles.Fill(links);
            _tree.Add(links);
            foreach (MetaNodeDefinition node in tree.Nodes)
                if (node != null) _tree.Add(NodeCard(node, centers[node]));
        }

        private VisualElement NodeCard(MetaNodeDefinition node, Rect rect) {
            int level = _progress.Level(node);
            string refusal = _progress.RefusalReason(node);
            bool locked = refusal != null && refusal.StartsWith("Nécessite");
            var card = new VisualElement();
            card.AddToClassList("ae-meta__node");
            card.EnableInClassList("ae-meta__node--owned", level > 0);
            card.EnableInClassList("ae-meta__node--locked", locked);
            card.EnableInClassList("ae-meta__node--max", level >= node.MaxLevel);
            card.style.position = Position.Absolute;
            card.style.left = rect.x;
            card.style.top = rect.y;
            card.style.width = rect.width;
            card.style.height = rect.height;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var icon = new VisualElement();
            icon.AddToClassList("ae-meta__icon");
            if (node.Icon != null) icon.style.backgroundImage = new StyleBackground(node.Icon);
            row.Add(icon);
            var texts = new VisualElement();
            texts.style.flexShrink = 1;
            var name = new Label(node.DisplayName);
            name.AddToClassList("ae-meta__name");
            texts.Add(name);
            var lvl = new Label($"Niveau {level}/{node.MaxLevel}");
            lvl.AddToClassList("ae-dim");
            lvl.style.fontSize = 11;
            texts.Add(lvl);
            row.Add(texts);
            card.Add(row);
            card.style.flexDirection = FlexDirection.Column;

            bool max = level >= node.MaxLevel;
            Button buy = MenuStyles.Button(max ? "Maximum" : $"Améliorer · {node.Cost(level + 1)}", () => _progress.TryBuy(node));
            buy.AddToClassList("ae-button--primary");
            buy.style.height = 26;
            buy.style.fontSize = 12;
            buy.style.marginTop = StyleKeyword.Auto;   // pinned to the bottom, under the level
            buy.SetEnabled(refusal == null);
            card.Add(buy);

            _tooltip.Attach(card, () => (node.DisplayName,
                max ? "Niveau maximum" : $"Niveau {level + 1} : {node.Cost(level + 1)} {_progress.Settings.CurrencyName}",
                $"{node.Description}\n\n{(level > 0 ? "Actuel : " + node.EffectText(level) + "\n" : "")}{(max ? "" : "Suivant : " + node.EffectText(level + 1))}"
                + (refusal != null && !max ? $"\n\n{refusal}" : "")));
            return card;
        }

        /// <summary>Lines from each node's prerequisites, gold when the prerequisite is owned.</summary>
        private sealed class LinkLayer : VisualElement {
            private readonly MetaTreeDefinition _tree;
            private readonly Dictionary<MetaNodeDefinition, Rect> _rects;
            private readonly MetaProgress _progress;

            public LinkLayer(MetaTreeDefinition tree, Dictionary<MetaNodeDefinition, Rect> rects, MetaProgress progress) {
                _tree = tree;
                _rects = rects;
                _progress = progress;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext ctx) {
                Painter2D p = ctx.painter2D;
                p.lineWidth = 3f;
                foreach (MetaNodeDefinition node in _tree.Nodes) {
                    if (node == null || !_rects.TryGetValue(node, out Rect to)) continue;
                    foreach (MetaNodeDefinition pre in node.Prerequisites) {
                        if (pre == null || !_rects.TryGetValue(pre, out Rect from)) continue;
                        bool lit = _progress.Level(pre) > 0;
                        p.strokeColor = lit ? new Color(0.56f, 0.5f, 1f, 0.95f) : new Color(0.24f, 0.25f, 0.42f, 0.7f);
                        var a = new Vector2(from.xMax, from.center.y);
                        var b = new Vector2(to.xMin, to.center.y);
                        float mid = (a.x + b.x) / 2f;
                        p.BeginPath();
                        p.MoveTo(a);
                        p.BezierCurveTo(new Vector2(mid, a.y), new Vector2(mid, b.y), b);
                        p.Stroke();
                    }
                }
            }
        }
    }
}
