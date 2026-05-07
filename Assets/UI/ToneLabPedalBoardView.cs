using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalBoardView
{
    private const float DragActivationPixels = 6f;
    private const float PedalTileSpacing = 18f;

    private readonly VisualElement root;
    private readonly VisualElement lane;
    private readonly VisualElement boardViewport;
    private readonly ScrollView chainScrollView;
    private readonly VisualElement chainRow;
    private readonly Label emptyLabel;

    private ToneLabPedalTile draggingTile;
    private ToneLabPedalTile dragPreviewTile;
    private Vector2 dragStartPosition;
    private int dragPointerId = -1;
    private bool dragMoved;
    private int dragTargetIndex = -1;

    public VisualElement Root => root;

    public event Action<string> PedalSelected;
    public event Action<string, bool> PedalEnabledChanged;
    public event Action<IReadOnlyList<string>> PedalOrderCommitted;
    public event Action AddPedalRequested;

    public ToneLabPedalBoardView()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.minHeight = 0f;
        root.style.paddingTop = 0f;
        root.style.paddingBottom = 0f;

        lane = new VisualElement();
        lane.style.flexGrow = 1f;
        lane.style.minHeight = 0f;
        lane.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        lane.style.borderTopWidth = 0f;
        lane.style.borderRightWidth = 0f;
        lane.style.borderBottomWidth = 0f;
        lane.style.borderLeftWidth = 0f;
        lane.style.borderTopLeftRadius = 0f;
        lane.style.borderTopRightRadius = 0f;
        lane.style.borderBottomLeftRadius = 0f;
        lane.style.borderBottomRightRadius = 0f;
        lane.style.paddingLeft = 0f;
        lane.style.paddingRight = 0f;
        lane.style.paddingTop = 0f;
        lane.style.paddingBottom = 0f;
        lane.style.justifyContent = Justify.FlexStart;
        lane.style.alignItems = Align.Stretch;
        lane.style.overflow = Overflow.Visible;
        root.Add(lane);

        VisualElement signalLine = new VisualElement();
        signalLine.style.position = Position.Absolute;
        signalLine.style.left = 24f;
        signalLine.style.right = 24f;
        signalLine.style.top = 90f;
        signalLine.style.height = 3f;
        signalLine.style.backgroundColor = new Color(0.76f, 0.62f, 0.42f, 0.12f);
        signalLine.style.borderTopLeftRadius = 2f;
        signalLine.style.borderTopRightRadius = 2f;
        signalLine.style.borderBottomLeftRadius = 2f;
        signalLine.style.borderBottomRightRadius = 2f;
        lane.Add(signalLine);

        VisualElement boardHeader = new VisualElement();
        boardHeader.style.position = Position.Absolute;
        boardHeader.style.left = 4f;
        boardHeader.style.right = 4f;
        boardHeader.style.top = 0f;
        boardHeader.style.flexDirection = FlexDirection.Row;
        boardHeader.style.justifyContent = Justify.SpaceBetween;
        boardHeader.style.alignItems = Align.FlexStart;
        lane.Add(boardHeader);

        VisualElement boardCopy = new VisualElement();
        boardCopy.style.flexGrow = 1f;
        boardCopy.style.maxWidth = 420f;
        boardCopy.style.marginRight = 18f;
        boardCopy.pickingMode = PickingMode.Ignore;
        boardHeader.Add(boardCopy);

        Label boardTitle = new Label("Pedalboard");
        boardTitle.style.color = Color.white;
        boardTitle.style.fontSize = 22f;
        boardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        boardCopy.Add(boardTitle);

        Label boardSubtitle = new Label("Select a pedal to edit settings, or drag to change order.");
        boardSubtitle.style.color = new Color(0.68f, 0.71f, 0.76f, 0.94f);
        boardSubtitle.style.fontSize = 13f;
        boardSubtitle.style.marginTop = 4f;
        boardSubtitle.style.whiteSpace = WhiteSpace.Normal;
        boardCopy.Add(boardSubtitle);

        Button addPedalButton = new Button(() => AddPedalRequested?.Invoke())
        {
            text = "+"
        };
        addPedalButton.style.width = 54f;
        addPedalButton.style.minWidth = 54f;
        addPedalButton.style.height = 54f;
        addPedalButton.style.flexShrink = 0f;
        addPedalButton.style.paddingLeft = 0f;
        addPedalButton.style.paddingRight = 0f;
        addPedalButton.style.paddingTop = 0f;
        addPedalButton.style.paddingBottom = 2f;
        addPedalButton.style.fontSize = 30f;
        addPedalButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        addPedalButton.style.color = new Color(0.97f, 0.98f, 1f, 0.98f);
        addPedalButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        addPedalButton.style.borderTopWidth = 1f;
        addPedalButton.style.borderRightWidth = 1f;
        addPedalButton.style.borderBottomWidth = 1f;
        addPedalButton.style.borderLeftWidth = 1f;
        addPedalButton.style.borderTopColor = new Color(1f, 1f, 1f, 0.92f);
        addPedalButton.style.borderRightColor = new Color(1f, 1f, 1f, 0.82f);
        addPedalButton.style.borderBottomColor = new Color(1f, 1f, 1f, 0.72f);
        addPedalButton.style.borderLeftColor = new Color(1f, 1f, 1f, 0.82f);
        addPedalButton.style.borderTopLeftRadius = 14f;
        addPedalButton.style.borderTopRightRadius = 14f;
        addPedalButton.style.borderBottomLeftRadius = 14f;
        addPedalButton.style.borderBottomRightRadius = 14f;
        boardHeader.Add(addPedalButton);

        boardViewport = new VisualElement();
        boardViewport.style.flexGrow = 1f;
        boardViewport.style.minHeight = 0f;
        boardViewport.style.marginTop = 72f;
        boardViewport.style.justifyContent = Justify.Center;
        boardViewport.style.alignItems = Align.Stretch;
        lane.Add(boardViewport);

        chainScrollView = new ScrollView(ScrollViewMode.Horizontal);
        chainScrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        chainScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        chainScrollView.style.flexGrow = 1f;
        chainScrollView.style.minHeight = 0f;
        chainScrollView.style.width = Length.Percent(100f);
        chainScrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        chainScrollView.style.borderTopWidth = 0f;
        chainScrollView.style.borderRightWidth = 0f;
        chainScrollView.style.borderBottomWidth = 0f;
        chainScrollView.style.borderLeftWidth = 0f;
        chainScrollView.style.paddingLeft = 0f;
        chainScrollView.style.paddingRight = 0f;
        chainScrollView.style.paddingTop = 0f;
        chainScrollView.style.paddingBottom = 0f;
        chainScrollView.contentViewport.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        chainScrollView.contentViewport.style.borderTopWidth = 0f;
        chainScrollView.contentViewport.style.borderRightWidth = 0f;
        chainScrollView.contentViewport.style.borderBottomWidth = 0f;
        chainScrollView.contentViewport.style.borderLeftWidth = 0f;
        chainScrollView.contentViewport.style.paddingBottom = 10f;
        chainScrollView.contentContainer.style.flexGrow = 1f;
        chainScrollView.contentContainer.style.minHeight = ToneLabPedalVisualBuilder.BoardTileHeight + 12f;
        chainScrollView.contentContainer.style.justifyContent = Justify.Center;
        chainScrollView.contentContainer.style.alignItems = Align.Center;
        ApplyScrollViewStyle(chainScrollView);
        boardViewport.Add(chainScrollView);

        chainRow = new VisualElement();
        chainRow.style.flexDirection = FlexDirection.Row;
        chainRow.style.alignItems = Align.FlexEnd;
        chainRow.style.justifyContent = Justify.Center;
        chainRow.style.alignSelf = Align.Center;
        chainRow.style.minHeight = ToneLabPedalVisualBuilder.BoardTileHeight;
        chainRow.style.minWidth = Length.Percent(100f);
        chainRow.style.paddingLeft = 8f;
        chainRow.style.paddingRight = 8f;
        chainRow.style.paddingBottom = 4f;
        chainScrollView.Add(chainRow);

        emptyLabel = new Label("No pedals in the chain.");
        emptyLabel.style.color = new Color(0.70f, 0.73f, 0.77f, 0.82f);
        emptyLabel.style.fontSize = 16f;
        emptyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        emptyLabel.style.display = DisplayStyle.None;
        emptyLabel.style.position = Position.Absolute;
        emptyLabel.style.left = 0f;
        emptyLabel.style.right = 0f;
        emptyLabel.style.top = 0f;
        emptyLabel.style.bottom = 18f;
        emptyLabel.style.alignSelf = Align.Center;
        lane.Add(emptyLabel);
    }

    public void Refresh(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain, string selectedPedalInstanceId)
    {
        draggingTile = null;
        DestroyDragPreview();
        dragPointerId = -1;
        dragMoved = false;
        dragTargetIndex = -1;
        ClearLiveGapOffsets();

        chainRow.Clear();
        if (pedalChain == null || pedalChain.Count == 0)
        {
            emptyLabel.style.display = DisplayStyle.Flex;
            chainScrollView.scrollOffset = Vector2.zero;
            return;
        }

        emptyLabel.style.display = DisplayStyle.None;
        for (int i = 0; i < pedalChain.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot == null)
                continue;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot.pedal_type);
            ToneLabPedalTile tile = new ToneLabPedalTile(slot.pedal_instance_id, descriptor);
            tile.SetPedalEnabledVisual(slot.enabled);
            tile.SetSelected(string.Equals(selectedPedalInstanceId, slot.pedal_instance_id, StringComparison.Ordinal));
            AttachTileInteractions(tile);
            chainRow.Add(tile);
        }
    }

    private void AttachTileInteractions(ToneLabPedalTile tile)
    {
        tile.BypassButton.clicked += () => PedalEnabledChanged?.Invoke(tile.PedalInstanceId, !tile.IsPedalEnabled);
        tile.RegisterCallback<PointerDownEvent>(evt => OnTilePointerDown(tile, evt), TrickleDown.TrickleDown);
        tile.RegisterCallback<PointerMoveEvent>(evt => OnTilePointerMove(tile, evt), TrickleDown.TrickleDown);
        tile.RegisterCallback<PointerUpEvent>(evt => OnTilePointerUp(tile, evt), TrickleDown.TrickleDown);
        tile.RegisterCallback<PointerCaptureOutEvent>(_ => ResetDraggingTile(tile));
    }

    private void OnTilePointerDown(ToneLabPedalTile tile, PointerDownEvent evt)
    {
        if (evt.button != 0 || draggingTile != null)
            return;

        evt.StopPropagation();
        draggingTile = tile;
        dragPointerId = evt.pointerId;
        dragStartPosition = evt.position;
        dragMoved = false;
        dragTargetIndex = GetTileIndex(tile);
        tile.CapturePointer(dragPointerId);
    }

    private void OnTilePointerMove(ToneLabPedalTile tile, PointerMoveEvent evt)
    {
        if (draggingTile != tile || dragPointerId != evt.pointerId || !tile.HasPointerCapture(dragPointerId))
            return;

        evt.StopPropagation();
        float deltaX = evt.position.x - dragStartPosition.x;
        if (!dragMoved && Mathf.Abs(deltaX) < DragActivationPixels)
            return;

        dragMoved = true;
        EnsureDragPreview(tile);
        UpdateDragPreviewPosition(tile, deltaX);
        dragTargetIndex = GetTargetIndex(evt.position.x, tile);
        UpdateLiveGapOffsets(tile, dragTargetIndex);
    }

    private void OnTilePointerUp(ToneLabPedalTile tile, PointerUpEvent evt)
    {
        if (draggingTile != tile || dragPointerId != evt.pointerId)
            return;

        evt.StopPropagation();
        if (tile.HasPointerCapture(dragPointerId))
            tile.ReleasePointer(dragPointerId);

        bool reordered = dragMoved;
        IReadOnlyList<string> committedOrder = reordered
            ? BuildCommittedOrder(tile.PedalInstanceId, dragTargetIndex)
            : null;
        ResetDraggingTile(tile);
        if (reordered && committedOrder != null && !OrdersMatch(committedOrder))
            PedalOrderCommitted?.Invoke(committedOrder);
        else
            PedalSelected?.Invoke(tile.PedalInstanceId);
    }

    private void ResetDraggingTile(ToneLabPedalTile tile)
    {
        tile.style.translate = new Translate(0f, 0f, 0f);
        tile.SetSourceHidden(false);
        tile.SetDragging(false);
        DestroyDragPreview();
        ClearLiveGapOffsets();
        draggingTile = null;
        dragPointerId = -1;
        dragMoved = false;
        dragTargetIndex = -1;
    }

    private int GetTargetIndex(float pointerX, ToneLabPedalTile draggedTile)
    {
        int insertionIndex = 0;
        for (int i = 0; i < chainRow.childCount; i++)
        {
            ToneLabPedalTile candidate = chainRow.ElementAt(i) as ToneLabPedalTile;
            if (candidate == null || candidate == draggedTile)
                continue;

            if (pointerX < candidate.worldBound.center.x)
                return insertionIndex;

            insertionIndex++;
        }

        return insertionIndex;
    }

    private void UpdateLiveGapOffsets(ToneLabPedalTile draggedTile, int targetIndex)
    {
        int originalIndex = GetTileIndex(draggedTile);
        float gapOffset = ToneLabPedalVisualBuilder.BoardTileWidth + PedalTileSpacing;
        for (int childIndex = 0; childIndex < chainRow.childCount; childIndex++)
        {
            ToneLabPedalTile candidate = chainRow.ElementAt(childIndex) as ToneLabPedalTile;
            if (candidate == null || ReferenceEquals(candidate, draggedTile))
                continue;

            float offsetX = 0f;
            if (targetIndex > originalIndex)
            {
                if (childIndex > originalIndex && childIndex <= targetIndex)
                    offsetX = -gapOffset;
            }
            else if (targetIndex < originalIndex)
            {
                if (childIndex >= targetIndex && childIndex < originalIndex)
                    offsetX = gapOffset;
            }

            candidate.style.translate = new Translate(offsetX, 0f, 0f);
        }
    }

    private void ClearLiveGapOffsets()
    {
        for (int childIndex = 0; childIndex < chainRow.childCount; childIndex++)
        {
            ToneLabPedalTile candidate = chainRow.ElementAt(childIndex) as ToneLabPedalTile;
            if (candidate == null || ReferenceEquals(candidate, draggingTile))
                continue;

            candidate.style.translate = new Translate(0f, 0f, 0f);
        }
    }

    private void EnsureDragPreview(ToneLabPedalTile tile)
    {
        if (dragPreviewTile != null)
            return;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(tile.PedalType);
        dragPreviewTile = new ToneLabPedalTile(tile.PedalInstanceId, descriptor);
        dragPreviewTile.pickingMode = PickingMode.Ignore;
        dragPreviewTile.style.position = Position.Absolute;
        dragPreviewTile.style.marginRight = 0f;
        dragPreviewTile.style.marginTop = 0f;
        dragPreviewTile.style.marginBottom = 0f;
        dragPreviewTile.SetPedalEnabledVisual(tile.IsPedalEnabled);
        dragPreviewTile.SetSelected(true);
        dragPreviewTile.SetDragging(true);
        lane.Add(dragPreviewTile);
        dragPreviewTile.BringToFront();
        tile.SetSourceHidden(true);
    }

    private void UpdateDragPreviewPosition(ToneLabPedalTile tile, float deltaX)
    {
        if (dragPreviewTile == null)
            return;

        dragPreviewTile.style.left = Mathf.Max(0f, (tile.worldBound.x - lane.worldBound.x) + deltaX);
        dragPreviewTile.style.top = Mathf.Max(0f, tile.worldBound.y - lane.worldBound.y);
    }

    private void DestroyDragPreview()
    {
        if (dragPreviewTile == null)
            return;

        dragPreviewTile.RemoveFromHierarchy();
        dragPreviewTile = null;
    }

    private int GetTileIndex(ToneLabPedalTile tile)
    {
        for (int i = 0; i < chainRow.childCount; i++)
        {
            if (ReferenceEquals(chainRow.ElementAt(i), tile))
                return i;
        }

        return -1;
    }

    private List<string> GetCurrentOrder()
    {
        List<string> ordered = new List<string>(chainRow.childCount);
        for (int i = 0; i < chainRow.childCount; i++)
        {
            ToneLabPedalTile tile = chainRow.ElementAt(i) as ToneLabPedalTile;
            if (tile != null)
                ordered.Add(tile.PedalInstanceId);
        }

        return ordered;
    }

    private List<string> BuildCommittedOrder(string draggedPedalInstanceId, int targetIndex)
    {
        List<string> ordered = GetCurrentOrder();
        ordered.Remove(draggedPedalInstanceId);
        int clampedTargetIndex = Mathf.Clamp(targetIndex, 0, ordered.Count);
        ordered.Insert(clampedTargetIndex, draggedPedalInstanceId);
        return ordered;
    }

    private bool OrdersMatch(IReadOnlyList<string> candidateOrder)
    {
        List<string> currentOrder = GetCurrentOrder();
        if (candidateOrder == null || candidateOrder.Count != currentOrder.Count)
            return false;

        for (int i = 0; i < currentOrder.Count; i++)
        {
            if (!string.Equals(candidateOrder[i], currentOrder[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static void ApplyScrollViewStyle(ScrollView scrollView)
    {
        scrollView.RegisterCallback<AttachToPanelEvent>(_ => StyleHorizontalScroller(scrollView));
        StyleHorizontalScroller(scrollView);
    }

    private static void StyleHorizontalScroller(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        Scroller scroller = scrollView.horizontalScroller;
        if (scroller == null)
            return;

        scroller.style.height = 10f;
        scroller.style.marginLeft = 34f;
        scroller.style.marginRight = 34f;
        scroller.style.marginTop = 10f;
        scroller.style.marginBottom = 4f;
        scroller.style.paddingLeft = 0f;
        scroller.style.paddingRight = 0f;
        scroller.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        scroller.style.borderTopWidth = 0f;
        scroller.style.borderRightWidth = 0f;
        scroller.style.borderBottomWidth = 0f;
        scroller.style.borderLeftWidth = 0f;

        VisualElement lowButton = scroller.Q<VisualElement>(className: "unity-scroller__low-button");
        VisualElement highButton = scroller.Q<VisualElement>(className: "unity-scroller__high-button");
        if (lowButton != null)
            lowButton.style.display = DisplayStyle.None;
        if (highButton != null)
            highButton.style.display = DisplayStyle.None;

        Slider slider = scroller.Q<Slider>();
        if (slider == null)
            return;

        slider.style.flexGrow = 1f;
        slider.style.height = 10f;
        slider.style.marginLeft = 0f;
        slider.style.marginRight = 0f;
        slider.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
        if (dragContainer != null)
        {
            dragContainer.style.height = 4f;
            dragContainer.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            dragContainer.style.borderTopLeftRadius = 999f;
            dragContainer.style.borderTopRightRadius = 999f;
            dragContainer.style.borderBottomLeftRadius = 999f;
            dragContainer.style.borderBottomRightRadius = 999f;
            dragContainer.style.marginTop = 3f;
        }

        VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
        if (tracker != null)
        {
            tracker.style.height = 4f;
            tracker.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            tracker.style.borderTopLeftRadius = 999f;
            tracker.style.borderTopRightRadius = 999f;
            tracker.style.borderBottomLeftRadius = 999f;
            tracker.style.borderBottomRightRadius = 999f;
        }

        VisualElement dragger = slider.Q<VisualElement>(className: "unity-base-slider__dragger");
        if (dragger != null)
        {
            dragger.style.height = 4f;
            dragger.style.minWidth = 72f;
            dragger.style.backgroundColor = new Color(1f, 1f, 1f, 0.72f);
            dragger.style.borderTopLeftRadius = 999f;
            dragger.style.borderTopRightRadius = 999f;
            dragger.style.borderBottomLeftRadius = 999f;
            dragger.style.borderBottomRightRadius = 999f;
            dragger.style.borderTopWidth = 0f;
            dragger.style.borderRightWidth = 0f;
            dragger.style.borderBottomWidth = 0f;
            dragger.style.borderLeftWidth = 0f;
        }
    }
}
