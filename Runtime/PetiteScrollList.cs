#define UI_COMPONENT_DEBUG
// #define UI_COMPONENT_VERBOSE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using DomUtil;
using UnityEngine.EventSystems;

public class PetiteScrollList : BindableElement {
    #region Factory boilerplate and attributes
    public new class UxmlFactory : UxmlFactory<PetiteScrollList, UxmlTraits> {}
    public new class UxmlTraits : BindableElement.UxmlTraits {
        // Defines the accepted child type.
        public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription {
            get {
                yield return new UxmlChildElementDescription(typeof(VisualElement));
            }
        }
        // Attributes
        UxmlBoolAttributeDescription barAutoHide = new() {
            name = "bar-auto-hide",
            defaultValue = true
        };
        UxmlBoolAttributeDescription smoothScroll = new() {
            name = "smooth-scroll",
            defaultValue = false
        };
        UxmlFloatAttributeDescription wheelRatio = new() {
            name = "wheel-ratio",
            defaultValue = 300,
            // No need to restrict wheel ratio:
            // negative value -> reverse rolling, zero -> no wheel rolling
        };
        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
            base.Init(ve, bag, cc);
            var el = (PetiteScrollList)ve;

            // declare attrs
            el.barAutoHide  = barAutoHide.GetValueFromBag(bag, cc);
            el.wheelRatio   = wheelRatio.GetValueFromBag(bag, cc);
            el.smoothScroll = smoothScroll.GetValueFromBag(bag, cc);
        }
    }
    #endregion
    // TODO: mouse swipe customable
    // TODO: style variables in attr as well
    // TODO: add temp class -> toggle class
    // TODO: enable vertical list variation
    // TODO: switch to BEM standard, <Name>__<Sub>--<State>

    static class StyleProps {
        public static readonly CustomStyleProperty<Length> barWidth  = new("--psl-bar-width");
        public static readonly CustomStyleProperty<Length> barRadius = new("--psl-bar-radius");
    }

    #region Classname constants & elem refs
    public static class ClassNames {
        // Classnames are identical to ids!
        public const string listRow      = "psl-list-row";
        public const string list         = "psl-list";
        public const string listViewport = "psl-list-viewport";
        public const string container    = "psl-container";
        public const string bar          = "psl-bar";
        public const string barTrack     = "psl-bar-track";
        public const string barContainer = "psl-bar-container";
        // Following classes are not linked to specific elements
        public const string barActive    = "psl-bar-active";
        public const string smoothScroll = "psl-smooth-scroll";
    }
    VisualElement list;
    VisualElement listViewport;
    VisualElement container;
    VisualElement bar;
    VisualElement barTrack;
    VisualElement barContainer;
    #endregion

    public override VisualElement contentContainer => this.list;

    #region Attrs
    public float wheelRatio { get; private set; } = 200f;
    public int maxChilds    { get; private set; } = int.MaxValue;
    public bool barAutoHide { get; private set; } = false;
    private bool _smoothScroll = false;
    public bool smoothScroll {
        get => _smoothScroll;
        private set {
            list.EnableInClassList(ClassNames.smoothScroll, value);
        }
    }
    #endregion

    public PetiteScrollList() {
        var asset = Resources.Load<VisualTreeAsset>("PetiteScrollList");
        asset.CloneTree(this);

        #region Assign elem refs
        list         = this.Q<VisualElement>(ClassNames.list);
        listViewport = this.Q<VisualElement>(ClassNames.listViewport);
        container    = this.Q<VisualElement>(ClassNames.container);
        bar          = this.Q<VisualElement>(ClassNames.bar);
        barTrack     = this.Q<VisualElement>(ClassNames.barTrack);
        barContainer = this.Q<VisualElement>(ClassNames.barContainer);
        #endregion
        RegisterCallback<CustomStyleResolvedEvent>(OnStylesResolved);
        initLayoutEvents();
        initPointerEvents();

        RegisterCallback<AttachToPanelEvent>(ev => {
            var visualTree = ev.destinationPanel.visualTree;
            Verbose($"AttachToPanel {visualTree.name}");
            initKeyboardEvents();
        });
        RegisterCallback<DetachFromPanelEvent>(ev => {
            Verbose($"DetachFromPanel {ev.originPanel.visualTree.name}");
        });
    }

    void OnStylesResolved(CustomStyleResolvedEvent ev) {
        if (ev.customStyle.TryGetPixelLength(StyleProps.barWidth, out var barWidth)) {
            barContainer.style.width = barWidth;  // Caution
        }
        if (ev.customStyle.TryGetPixelLength(StyleProps.barRadius, out var barRadius)) {
            bar     .style.setBorderRadius(barRadius);
            barTrack.style.setBorderRadius(barRadius);
        }
    }

    // TODO: Move event init actions into manipulator class
    class ScrollerPointerManipulator : PointerManipulator {
        protected override void RegisterCallbacksOnTarget() {
            //
        }
        protected override void UnregisterCallbacksFromTarget() {
            //
        }
    }

    bool isFirstMove = false;
    void initPointerEvents() {
        // mouse/touch swipe
        EventCallback<PointerMoveEvent> onMove = (ev) => {
            if (isFirstMove) {
                PointerCaptureHelper.CapturePointer(list, ev.pointerId);
                isFirstMove = false;
            }
            contentPosition -= ev.deltaPosition.y;
            updateList(true);
        };
        Action release = null;
        EventCallback<PointerUpEvent> onUp = (ev) => {
            PointerCaptureHelper.ReleasePointer(list, ev.pointerId);
            release();
        };
        release = () => {
            list.UnregisterCallback(onMove);
            list.UnregisterCallback(onUp);
        };
        list.RegisterCallback<PointerDownEvent>(ev => {
            PointerCaptureHelper.CapturePointer(list, ev.pointerId);
            isFirstMove = true;
            list.RegisterCallback(onMove);
            list.RegisterCallback(onUp);
        });

        // mouse scroll
        listViewport.RegisterCallback<WheelEvent>(ev => {
            ev.StopPropagation();
            contentPosition += ev.delta.y * wheelRatio;
            updateList(true);
        });

        // scrollbar
        float pointerStart = 0;
        EventCallback<PointerMoveEvent> barMove = (ev) => {
            barPosition += ev.deltaPosition.y;
            updateBar(true);
        };
        Action barRelease = null;
        EventCallback<PointerUpEvent> barUp = (ev) => {
            PointerCaptureHelper.ReleasePointer(bar, ev.pointerId);
            bar.RemoveFromClassList(ClassNames.barActive);
            barRelease();
        };
        barRelease = () => {
            bar.UnregisterCallback(barMove);
            bar.UnregisterCallback(barUp);
        };
        bar.RegisterCallback<PointerDownEvent>((ev) => {
            PointerCaptureHelper.CapturePointer(bar, ev.pointerId);
            pointerStart = ev.position.y;
            bar.RegisterCallback(barMove);
            bar.RegisterCallback(barUp);
            bar.AddToClassList(ClassNames.barActive);
        });
    }

    void initKeyboardEvents() {
        list.RegisterCallback<NavigationMoveEvent>(ev => {
            var direction = ev.direction;
            Verbose(direction);
            Verbose(ev.move);
            if (
                direction >= NavigationMoveEvent.Direction.Left &&
                direction <= NavigationMoveEvent.Direction.Down
            ) ev.PreventDefault();
        });
        list.RegisterCallback<KeyDownEvent>(ev => {
            var keyCode = ev.keyCode;
            bool isDown  = (keyCode == KeyCode.DownArrow);
            bool isUp    = (keyCode == KeyCode.UpArrow);
            bool isRight = (keyCode == KeyCode.RightArrow);
            bool isLeft  = (keyCode == KeyCode.LeftArrow);
            Vector2 keyboardVector = new Vector2(
                Convert.ToInt32(isRight) - Convert.ToInt32(isLeft),
                Convert.ToInt32(isDown) - Convert.ToInt32(isUp)
            ).normalized;
            Verbose($"KeyDown: " + ev.keyCode);
            contentPosition += keyboardVector.y * wheelRatio;
            updateList(true);
        });
    }

    #region Position variables
    float listHeight = 0;
    float listViewportHeight = 0;
    float contentPosition = 0;
    float contentRange = 0;

    float barPosition = 0;
    float barLength = 0;
    float barTotal = 0;
    float barRange = 0;
    #endregion

    bool dirtyBar = false;
    void initLayoutEvents() {
        list.RegisterCallback<GeometryChangedEvent>(ev => {
            Verbose("list changed");
            listHeight = ev.newRect.height;
            listViewportHeight = listViewport.layout.height;
            contentRange = listHeight - listViewportHeight;
            updateList();
            setBarLength(listViewportHeight / listHeight);
            // list.schedule.Execute(timer => {
            //     barLength = bar.layout.height;
            //     barTotal = barTrack.layout.height;
            //     barRange = barTotal - barLength;
            //     Verbose($"{barLength} {barTotal} {barRange}");
            //     updateBar();
            // });
        });
        bar.RegisterCallback<GeometryChangedEvent>(ev => {
            if (dirtyBar) {
                dirtyBar = false;
                Verbose("bar dirty, skipped");
                return;
            }
            Verbose("scrollbar geometry changed");
            barLength = bar.layout.height;
            barTotal = barTrack.layout.height;
            barRange = barTotal - barLength;
            Verbose($"{barLength} {barTotal} {barRange}");
            updateBar();
        });
        listViewport.RegisterCallback<GeometryChangedEvent>(ev => {
            Verbose("listViewport geometry changed");
            dirtyBar = true;
        });
        list.usageHints = UsageHints.GroupTransform;
        barTrack.usageHints = UsageHints.GroupTransform;
    }
    void updateList(bool syncBar = false) {
        contentPosition = Mathf.Clamp(contentPosition, 0, contentRange);
        list.transform.position = contentPosition * Vector3.down;
        if (syncBar) {
            barPosition = (contentRange <= 0) ? 0 :
                contentPosition / contentRange * barRange;
            updateBar();
        }
    }
    void updateBar(bool syncList = false) {
        barPosition = (barRange <= 0) ? 0 : Mathf.Clamp(barPosition, 0, barRange);
        bar.transform.position = Vector3.up * barPosition;
        if (syncList) {
            contentPosition = (barRange <= 0) ? 0 :
                barPosition / barRange * contentRange;
            updateList();
        }
    }
    void setBarLength(float progress) {
        progress = Mathf.Clamp01(progress);
        bar.style.height = Length.Percent(100 * progress);
        barTrack.style.visibility =
            (progress >= 1) ? Visibility.Hidden : Visibility.Visible;
        bar.MarkDirtyRepaint();
    }

    #region Essential public methods
    /// <summary>
    /// Append element, and assign classname {Classnames.listRow} to it.
    /// </summary>
    /// <param name="item"></param>
    public void AddRow(VisualElement item) {
        item.AddToClassList(ClassNames.listRow);
        Add(item);
    }
    /// <summary>
    /// Insert element (attach class)
    /// </summary>
    /// <param name="index"></param>
    /// <param name="element"></param>
    public void InsertRow(int index, VisualElement element) {
        element.AddToClassList(ClassNames.listRow);
        base.Insert(index, element);
    }
    /// <summary>
    /// Insert element (without attach class)
    /// </summary>
    /// <param name="index"></param>
    /// <param name="element"></param>

    public void ScrollToBottom() {
        ScrollTo(float.PositiveInfinity);
    }
    public void ScrollTo(float pos) {
        contentPosition = pos;
        barPosition = pos;
        list.MarkDirtyRepaint();
    }
    public void ScrollTo(VisualElement child) {
        if (list.FindCommonAncestor(child) == list) {
            var pos = 0;
            contentPosition = pos;
            barPosition = pos;
            list.MarkDirtyRepaint();
        }
    }
    #endregion

    #region Debug utilities
    static void Trace(object message) {
        #if UI_COMPONENT_DEBUG
        Debug.Log(message);
        #endif
    }
    static void Warn(object message) {
        #if UI_COMPONENT_DEBUG
        Debug.LogWarning(message);
        #endif
    }
    static void Verbose(object message) {
        #if UI_COMPONENT_DEBUG && UI_COMPONENT_VERBOSE
        Debug.Log(message);
        #endif
    }
    #endregion
}
