using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalBoardView
{
    private const float DragActivationPixels = 7f;
    private const float PedalTileSpacing = 18f;
    private static Texture2D addEffectGradientTexture;

    private readonly VisualElement root;
    private readonly VisualElement headerControls;
    private readonly ScrollView chainScrollView;
    private readonly VisualElement chainGrid;
    private readonly Label emptyLabel;

    private ToneLabPedalTile draggingTile;
    private ToneLabPedalTile dragPreviewTile;
    private VisualElement dragPlaceholder;
    private Vector2 dragStartPosition;
    private int dragPointerId = -1;
    private bool dragMoved;
    private int dragTargetIndex = -1;

    public VisualElement Root => root;
    public VisualElement HeaderControls => headerControls;

    public event Action<string> PedalSelected;
    public event Action<string, bool> PedalEnabledChanged;
    public event Action<string> PedalRemoveRequested;
    public event Action<IReadOnlyList<string>> PedalOrderCommitted;
    public event Action AddPedalRequested;

    public ToneLabPedalBoardView()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.minHeight = 0f;
        root.style.flexDirection = FlexDirection.Column;

        VisualElement boardHeader = new VisualElement();
        boardHeader.style.height = 78f;
        boardHeader.style.flexShrink = 0f;
        boardHeader.style.flexDirection = FlexDirection.Row;
        boardHeader.style.alignItems = Align.Center;
        boardHeader.style.justifyContent = Justify.SpaceBetween;
        boardHeader.style.borderBottomWidth = 1f;
        boardHeader.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
        boardHeader.style.marginBottom = 10f;
        root.Add(boardHeader);

        VisualElement copyColumn = new VisualElement();
        copyColumn.style.flexGrow = 1f;
        copyColumn.style.flexShrink = 1f;
        copyColumn.style.minWidth = 170f;
        copyColumn.style.translate = new Translate(0f, -3f, 0f);
        boardHeader.Add(copyColumn);

        Label boardTitle = new Label("Pedalboard");
        boardTitle.style.color = Color.white;
        boardTitle.style.fontSize = 24f;
        boardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        copyColumn.Add(boardTitle);

        Label boardSubtitle = new Label("Signal flows left to right, then wraps to the next row.");
        boardSubtitle.style.color = new Color(0.86f, 0.88f, 0.92f, 0.66f);
        boardSubtitle.style.fontSize = 12f;
        boardSubtitle.style.marginTop = 0f;
        copyColumn.Add(boardSubtitle);

        headerControls = new VisualElement();
        headerControls.style.flexDirection = FlexDirection.Row;
        headerControls.style.alignItems = Align.Center;
        headerControls.style.justifyContent = Justify.FlexEnd;
        headerControls.style.flexGrow = 1f;
        headerControls.style.flexShrink = 1f;
        headerControls.style.minWidth = 0f;
        headerControls.style.marginLeft = 18f;
        headerControls.style.marginRight = 18f;
        boardHeader.Add(headerControls);

        Button addPedalButton = new Button(() => AddPedalRequested?.Invoke())
        {
            text = "+ ADD EFFECT"
        };
        addPedalButton.style.width = 198f;
        addPedalButton.style.minWidth = 198f;
        addPedalButton.style.height = 52f;
        addPedalButton.style.marginRight = 0f;
        addPedalButton.style.paddingLeft = 18f;
        addPedalButton.style.paddingRight = 18f;
        addPedalButton.style.paddingTop = 0f;
        addPedalButton.style.paddingBottom = 0f;
        addPedalButton.style.fontSize = 18f;
        addPedalButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        addPedalButton.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.92f);
        addPedalButton.style.backgroundImage = new StyleBackground(GetAddEffectGradientTexture());
        addPedalButton.style.color = Color.white;
        addPedalButton.style.borderTopWidth = 1f;
        addPedalButton.style.borderRightWidth = 1f;
        addPedalButton.style.borderBottomWidth = 1f;
        addPedalButton.style.borderLeftWidth = 1f;
        addPedalButton.style.borderTopColor = new Color(1f, 0.92f, 0.64f, 0.80f);
        addPedalButton.style.borderRightColor = new Color(1f, 0.64f, 0.48f, 0.78f);
        addPedalButton.style.borderBottomColor = new Color(0.84f, 0.34f, 0.24f, 0.74f);
        addPedalButton.style.borderLeftColor = new Color(1f, 0.78f, 0.24f, 0.78f);
        addPedalButton.style.borderTopLeftRadius = 10f;
        addPedalButton.style.borderTopRightRadius = 10f;
        addPedalButton.style.borderBottomLeftRadius = 10f;
        addPedalButton.style.borderBottomRightRadius = 10f;
        addPedalButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            addPedalButton.style.unityBackgroundImageTintColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            addPedalButton.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f));
        });
        addPedalButton.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            addPedalButton.style.unityBackgroundImageTintColor = Color.white;
            addPedalButton.style.scale = new Scale(Vector3.one);
        });
        boardHeader.Add(addPedalButton);

        chainScrollView = new ScrollView(ScrollViewMode.Vertical);
        chainScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        chainScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        chainScrollView.style.flexGrow = 1f;
        chainScrollView.style.minHeight = 0f;
        chainScrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        chainScrollView.style.borderTopWidth = 0f;
        chainScrollView.style.borderRightWidth = 0f;
        chainScrollView.style.borderBottomWidth = 0f;
        chainScrollView.style.borderLeftWidth = 0f;
        chainScrollView.contentViewport.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        chainScrollView.contentContainer.style.flexGrow = 1f;
        root.Add(chainScrollView);

        chainGrid = new VisualElement();
        chainGrid.style.flexDirection = FlexDirection.Row;
        chainGrid.style.flexWrap = Wrap.Wrap;
        chainGrid.style.alignItems = Align.FlexStart;
        chainGrid.style.alignContent = Align.FlexStart;
        chainGrid.style.justifyContent = Justify.FlexStart;
        chainGrid.style.paddingTop = 8f;
        chainGrid.style.paddingLeft = 2f;
        chainGrid.style.paddingRight = 2f;
        chainGrid.style.paddingBottom = 28f;
        chainScrollView.Add(chainGrid);

        emptyLabel = new Label("No pedals in the chain. Open the library or press + to add one.");
        emptyLabel.style.display = DisplayStyle.None;
        emptyLabel.style.color = new Color(0.92f, 0.94f, 0.98f, 0.72f);
        emptyLabel.style.fontSize = 17f;
        emptyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        emptyLabel.style.marginTop = 90f;
        emptyLabel.style.whiteSpace = WhiteSpace.Normal;
        chainGrid.Add(emptyLabel);
    }

    public void Refresh(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain, string selectedPedalInstanceId)
    {
        ResetDragState();
        chainGrid.Clear();

        if (pedalChain == null || pedalChain.Count == 0)
        {
            emptyLabel.style.display = DisplayStyle.Flex;
            chainGrid.Add(emptyLabel);
            chainScrollView.scrollOffset = Vector2.zero;
            return;
        }

        emptyLabel.style.display = DisplayStyle.None;
        for (int i = 0; i < pedalChain.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot == null)
                continue;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
            ToneLabPedalTile tile = new ToneLabPedalTile(slot.pedal_instance_id, descriptor);
            tile.SetPedalEnabledVisual(slot.enabled);
            tile.SetSelected(string.Equals(selectedPedalInstanceId, slot.pedal_instance_id, StringComparison.Ordinal));
            AttachTileInteractions(tile);
            chainGrid.Add(tile);
        }
    }

    public int GetEstimatedColumns()
    {
        float availableWidth = chainScrollView?.contentViewport?.resolvedStyle.width ?? root.resolvedStyle.width;
        if (!float.IsFinite(availableWidth) || availableWidth <= 1f)
            availableWidth = root.resolvedStyle.width;
        if (!float.IsFinite(availableWidth) || availableWidth <= 1f)
            availableWidth = Screen.width;

        float tileStride = ToneLabPedalVisualBuilder.BoardTileWidth + PedalTileSpacing;
        return Mathf.Max(1, Mathf.FloorToInt((availableWidth + PedalTileSpacing) / tileStride));
    }

    public void ScrollPedalIntoView(string pedalInstanceId)
    {
        if (string.IsNullOrWhiteSpace(pedalInstanceId) || chainScrollView == null)
            return;

        for (int i = 0; i < chainGrid.childCount; i++)
        {
            if (chainGrid.ElementAt(i) is ToneLabPedalTile tile &&
                string.Equals(tile.PedalInstanceId, pedalInstanceId, StringComparison.Ordinal))
            {
                chainScrollView.ScrollTo(tile);
                return;
            }
        }
    }

    public int GetInsertionIndex(Vector2 panelPosition)
    {
        if (!root.worldBound.Contains(panelPosition))
            return -1;

        return GetTargetIndex(panelPosition, null);
    }

    private void AttachTileInteractions(ToneLabPedalTile tile)
    {
        tile.BypassButton.clicked += () => PedalEnabledChanged?.Invoke(tile.PedalInstanceId, !tile.IsPedalEnabled);
        tile.DeleteButton.clicked += () => PedalRemoveRequested?.Invoke(tile.PedalInstanceId);
        tile.RegisterCallback<PointerDownEvent>(evt => OnTilePointerDown(tile, evt));
        tile.RegisterCallback<PointerMoveEvent>(evt => OnTilePointerMove(tile, evt));
        tile.RegisterCallback<PointerUpEvent>(evt => OnTilePointerUp(tile, evt));
        tile.RegisterCallback<PointerCaptureOutEvent>(_ => ResetDraggingTile(tile));
    }

    private void OnTilePointerDown(ToneLabPedalTile tile, PointerDownEvent evt)
    {
        if (evt.button != 0 || draggingTile != null)
            return;

        evt.StopPropagation();
        draggingTile = tile;
        dragPointerId = evt.pointerId;
        dragStartPosition = new Vector2(evt.position.x, evt.position.y);
        dragMoved = false;
        dragTargetIndex = GetTileFlowIndex(tile);
        tile.CapturePointer(dragPointerId);
    }

    private void OnTilePointerMove(ToneLabPedalTile tile, PointerMoveEvent evt)
    {
        if (draggingTile != tile || dragPointerId != evt.pointerId || !tile.HasPointerCapture(dragPointerId))
            return;

        evt.StopPropagation();
        Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
        Vector2 delta = pointerPosition - dragStartPosition;
        if (!dragMoved && delta.magnitude < DragActivationPixels)
            return;

        if (!dragMoved)
            BeginDrag(tile);

        dragMoved = true;
        UpdateDragPreviewPosition(pointerPosition);
        dragTargetIndex = GetTargetIndex(pointerPosition, tile);
        MovePlaceholderToIndex(dragTargetIndex);
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
            ? BuildCommittedOrder(tile.PedalInstanceId, GetPlaceholderIndex())
            : null;
        ResetDraggingTile(tile);

        if (reordered && committedOrder != null && !OrdersMatch(committedOrder))
            PedalOrderCommitted?.Invoke(committedOrder);
        else if (!reordered)
            PedalSelected?.Invoke(tile.PedalInstanceId);
    }

    private void BeginDrag(ToneLabPedalTile tile)
    {
        EnsureDragPreview(tile);
        EnsureDragPlaceholder();
        tile.style.display = DisplayStyle.None;
        MovePlaceholderToIndex(GetTileFlowIndex(tile));
    }

    private void ResetDraggingTile(ToneLabPedalTile tile)
    {
        if (tile != null)
        {
            tile.style.display = DisplayStyle.Flex;
            tile.SetSourceHidden(false);
            tile.SetDragging(false);
        }

        ResetDragState();
    }

    private void ResetDragState()
    {
        DestroyDragPreview();
        DestroyDragPlaceholder();
        draggingTile = null;
        dragPointerId = -1;
        dragMoved = false;
        dragTargetIndex = -1;
    }

    private int GetTargetIndex(Vector2 panelPosition, ToneLabPedalTile draggedTile)
    {
        int insertionIndex = 0;
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            ToneLabPedalTile candidate = chainGrid.ElementAt(i) as ToneLabPedalTile;
            if (candidate == null || candidate == draggedTile)
                continue;

            Rect bounds = candidate.worldBound;
            if (bounds.width <= 1f || bounds.height <= 1f)
                continue;

            if (panelPosition.y < bounds.yMin)
                return insertionIndex;

            if (panelPosition.y <= bounds.yMax)
            {
                if (panelPosition.x < bounds.center.x)
                    return insertionIndex;

                insertionIndex++;
                continue;
            }

            insertionIndex++;
        }

        return insertionIndex;
    }

    private void EnsureDragPreview(ToneLabPedalTile tile)
    {
        if (dragPreviewTile != null)
            return;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(tile.DescriptorId);
        dragPreviewTile = new ToneLabPedalTile(tile.PedalInstanceId, descriptor);
        dragPreviewTile.pickingMode = PickingMode.Ignore;
        dragPreviewTile.style.position = Position.Absolute;
        dragPreviewTile.style.marginRight = 0f;
        dragPreviewTile.style.marginTop = 0f;
        dragPreviewTile.style.marginBottom = 0f;
        dragPreviewTile.SetPedalEnabledVisual(tile.IsPedalEnabled);
        dragPreviewTile.SetSelected(true);
        dragPreviewTile.SetDragging(true);
        root.Add(dragPreviewTile);
        dragPreviewTile.BringToFront();
        tile.SetSourceHidden(true);
    }

    private void UpdateDragPreviewPosition(Vector2 panelPosition)
    {
        if (dragPreviewTile == null)
            return;

        dragPreviewTile.style.left = Mathf.Max(0f, panelPosition.x - root.worldBound.x - (ToneLabPedalVisualBuilder.BoardTileWidth * 0.5f));
        dragPreviewTile.style.top = Mathf.Max(0f, panelPosition.y - root.worldBound.y - (ToneLabPedalVisualBuilder.BoardTileHeight * 0.5f));
    }

    private void DestroyDragPreview()
    {
        if (dragPreviewTile == null)
            return;

        dragPreviewTile.RemoveFromHierarchy();
        dragPreviewTile = null;
    }

    private void EnsureDragPlaceholder()
    {
        if (dragPlaceholder != null)
            return;

        dragPlaceholder = new VisualElement();
        dragPlaceholder.name = "tone-lab-pedal-placeholder";
        dragPlaceholder.style.width = ToneLabPedalVisualBuilder.BoardTileWidth;
        dragPlaceholder.style.minWidth = ToneLabPedalVisualBuilder.BoardTileWidth;
        dragPlaceholder.style.height = ToneLabPedalVisualBuilder.BoardTileHeight;
        dragPlaceholder.style.marginRight = PedalTileSpacing;
        dragPlaceholder.style.marginTop = 4f;
        dragPlaceholder.style.marginBottom = 22f;
        dragPlaceholder.style.borderTopWidth = 1f;
        dragPlaceholder.style.borderRightWidth = 1f;
        dragPlaceholder.style.borderBottomWidth = 1f;
        dragPlaceholder.style.borderLeftWidth = 1f;
        dragPlaceholder.style.borderTopColor = new Color(1f, 1f, 1f, 0.36f);
        dragPlaceholder.style.borderRightColor = new Color(1f, 1f, 1f, 0.20f);
        dragPlaceholder.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);
        dragPlaceholder.style.borderLeftColor = new Color(1f, 1f, 1f, 0.20f);
        dragPlaceholder.style.borderTopLeftRadius = 18f;
        dragPlaceholder.style.borderTopRightRadius = 18f;
        dragPlaceholder.style.borderBottomLeftRadius = 13f;
        dragPlaceholder.style.borderBottomRightRadius = 13f;
        dragPlaceholder.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
    }

    private void MovePlaceholderToIndex(int targetIndex)
    {
        if (dragPlaceholder == null)
            return;

        int clampedIndex = Mathf.Clamp(targetIndex, 0, GetFlowTileCount());
        if (dragPlaceholder.parent != null)
            dragPlaceholder.RemoveFromHierarchy();

        int childIndex = GetChildInsertionIndexForFlowIndex(clampedIndex);
        chainGrid.Insert(childIndex, dragPlaceholder);
    }

    private void DestroyDragPlaceholder()
    {
        if (dragPlaceholder == null)
            return;

        dragPlaceholder.RemoveFromHierarchy();
        dragPlaceholder = null;
    }

    private int GetPlaceholderIndex()
    {
        if (dragPlaceholder == null || dragPlaceholder.parent == null)
            return dragTargetIndex;

        int flowIndex = 0;
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            VisualElement child = chainGrid.ElementAt(i);
            if (child == dragPlaceholder)
                return flowIndex;

            if (child is ToneLabPedalTile tile && tile != draggingTile)
                flowIndex++;
        }

        return flowIndex;
    }

    private int GetChildInsertionIndexForFlowIndex(int flowIndex)
    {
        int seen = 0;
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            VisualElement child = chainGrid.ElementAt(i);
            if (child == dragPlaceholder)
                continue;

            if (child is ToneLabPedalTile tile && tile != draggingTile)
            {
                if (seen >= flowIndex)
                    return i;
                seen++;
            }
        }

        return chainGrid.childCount;
    }

    private int GetFlowTileCount()
    {
        int count = 0;
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            if (chainGrid.ElementAt(i) is ToneLabPedalTile tile && tile != draggingTile)
                count++;
        }

        return count;
    }

    private int GetTileFlowIndex(ToneLabPedalTile tile)
    {
        int index = 0;
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            ToneLabPedalTile candidate = chainGrid.ElementAt(i) as ToneLabPedalTile;
            if (candidate == null)
                continue;

            if (ReferenceEquals(candidate, tile))
                return index;

            index++;
        }

        return index;
    }

    private List<string> GetCurrentOrder()
    {
        List<string> ordered = new List<string>(chainGrid.childCount);
        for (int i = 0; i < chainGrid.childCount; i++)
        {
            ToneLabPedalTile tile = chainGrid.ElementAt(i) as ToneLabPedalTile;
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

    private static Texture2D GetAddEffectGradientTexture()
    {
        if (addEffectGradientTexture != null)
            return addEffectGradientTexture;

        const int width = 96;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "ToneLabAddEffectGradient",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color left = new Color(0.95f, 0.67f, 0.00f, 0.98f);
        Color right = new Color(1.00f, 0.38f, 0.45f, 0.98f);
        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            texture.SetPixel(x, 0, Color.Lerp(left, right, t));
        }

        texture.Apply(false, true);
        addEffectGradientTexture = texture;
        return addEffectGradientTexture;
    }
}
