using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using RectangleF = ExileCore2.Shared.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace KillCounter2;

/// <summary>
/// Simple per-area kill counter for PoE2 ExileCore2.
/// Counts dead hostile monsters by rarity and draws a small overlay.
/// Supports vertical and horizontal layouts plus per-rarity visibility.
/// </summary>
public class KillCounterPlugin : BaseSettingsPlugin<KillCounterSettings>
{
    private readonly Dictionary<MonsterRarity, int> _killsByRarity = new()
    {
        [MonsterRarity.White] = 0,
        [MonsterRarity.Magic] = 0,
        [MonsterRarity.Rare] = 0,
        [MonsterRarity.Unique] = 0
    };

    private readonly HashSet<long> _countedMonsterIds = new();
    private uint _currentAreaHash;

    private static readonly Color BackgroundColor = Color.FromArgb(140, 0, 0, 0);
    private const int BackgroundPadding = 5;

    public override bool Initialise()
    {
        Name = "KillCounter";
        CanUseMultiThreading = false; // counting is cheap and done on main thread
        ResetState(GameController.Area?.CurrentArea);
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        ResetState(area);
    }

    private void ResetState(AreaInstance? area)
    {
        _currentAreaHash = area?.Hash ?? 0;
        _countedMonsterIds.Clear();

        _killsByRarity[MonsterRarity.White] = 0;
        _killsByRarity[MonsterRarity.Magic] = 0;
        _killsByRarity[MonsterRarity.Rare] = 0;
        _killsByRarity[MonsterRarity.Unique] = 0;
    }

    public override void Render()
    {
        if (!Settings.Enable.Value)
            return;

        var area = GameController.Area?.CurrentArea;
        if (area == null)
            return;

        if (!Settings.ShowInTown.Value && (area.IsTown || area.IsHideout))
            return;

        if (!GameController.InGame)
            return;

        // Ensure we reset if we somehow missed AreaChange (safety net).
        if (area.Hash != _currentAreaHash)
            ResetState(area);

        UpdateCounters();

        var totalKills = GetTotalKills();
        if (totalKills == 0 && !Settings.ShowWhenZero.Value)
            return;

        DrawOverlay(totalKills);
    }

    private void UpdateCounters()
    {
        var entityListWrapper = GameController.EntityListWrapper;
        if (entityListWrapper == null)
            return;

        var validByType = entityListWrapper.ValidEntitiesByType;
        if (validByType == null)
            return;

        if (!validByType.TryGetValue(EntityType.Monster, out var monsters) || monsters == null)
            return;

        // Hot path: avoid LINQ and unnecessary allocations.
        foreach (var entity in monsters)
        {
            if (entity == null)
                continue;

            if (entity.IsAlive)
                continue;

            if (!entity.IsHostile)
                continue;

            if (!entity.HasComponent<ObjectMagicProperties>())
                continue;

            var rarity = entity.Rarity;
            if (rarity < MonsterRarity.White || rarity > MonsterRarity.Unique)
                continue;

            // Entity.Id type is UInt32 in ExileCore; normalize to Int64 for the hash set.
            long id = unchecked((long)entity.Id);
            if (!_countedMonsterIds.Add(id))
                continue;

            if (_killsByRarity.ContainsKey(rarity))
                _killsByRarity[rarity]++;
        }
    }

    private int GetTotalKills()
    {
        // Explicit sum to avoid allocations.
        return _killsByRarity[MonsterRarity.White]
             + _killsByRarity[MonsterRarity.Magic]
             + _killsByRarity[MonsterRarity.Rare]
             + _killsByRarity[MonsterRarity.Unique];
    }

    private void DrawOverlay(int totalKills)
    {
        if (Settings.HorizontalLayout.Value)
            DrawOverlayHorizontal(totalKills);
        else
            DrawOverlayVertical(totalKills);
    }

    #region Vertical layout

    private void DrawOverlayVertical(int totalKills)
    {
        var x = Settings.PositionX.Value;
        var y = Settings.PositionY.Value;

        var labelColor = Settings.TextColor.Value;

        // Prepare line texts for background measurement
        var lines = new List<string>
        {
            $"Kills: {totalKills}"
        };

        if (Settings.ShowDetail.Value)
        {
            if (Settings.ShowWhite.Value)
                lines.Add(GetRarityLineText("White", _killsByRarity[MonsterRarity.White]));
            if (Settings.ShowMagic.Value)
                lines.Add(GetRarityLineText("Magic", _killsByRarity[MonsterRarity.Magic]));
            if (Settings.ShowRare.Value)
                lines.Add(GetRarityLineText("Rare", _killsByRarity[MonsterRarity.Rare]));
            if (Settings.ShowUnique.Value)
                lines.Add(GetRarityLineText("Unique", _killsByRarity[MonsterRarity.Unique]));
        }

        DrawBackground(x, y, lines);

        // Actual drawing
        var pos = new Vector2(x, y);

        var headerText = $"Kills: {totalKills}";
        var headerSize = Graphics.DrawText(headerText, pos, labelColor, FontAlign.Left);
        pos.Y += headerSize.Y;

        if (!Settings.ShowDetail.Value)
            return;

        pos.Y += 2;

        if (Settings.ShowWhite.Value)
            pos = DrawRarityLine(pos, "White", _killsByRarity[MonsterRarity.White], Settings.WhiteColor.Value);

        if (Settings.ShowMagic.Value)
            pos = DrawRarityLine(pos, "Magic", _killsByRarity[MonsterRarity.Magic], Settings.MagicColor.Value);

        if (Settings.ShowRare.Value)
            pos = DrawRarityLine(pos, "Rare", _killsByRarity[MonsterRarity.Rare], Settings.RareColor.Value);

        if (Settings.ShowUnique.Value)
            _ = DrawRarityLine(pos, "Unique", _killsByRarity[MonsterRarity.Unique], Settings.UniqueColor.Value);
    }

    private static string GetRarityLineText(string label, int count)
    {
        return $"{label}: {count}";
    }

    private Vector2 DrawRarityLine(Vector2 pos, string label, int count, Color valueColor)
    {
        // Draw label first, then value right after it using returned size.
        var labelText = $"{label}: ";
        var labelSize = Graphics.DrawText(labelText, pos, Settings.TextColor.Value, FontAlign.Left);

        var valuePos = new Vector2(pos.X + labelSize.X, pos.Y);
        var valueText = count.ToString();
        var valueSize = Graphics.DrawText(valueText, valuePos, valueColor, FontAlign.Left);

        // Move Y down by the tallest part of the line.
        var lineHeight = Math.Max(labelSize.Y, valueSize.Y);
        return new Vector2(pos.X, pos.Y + lineHeight);
    }

    #endregion

    #region Horizontal layout

    private void DrawOverlayHorizontal(int totalKills)
    {
        var x = Settings.PositionX.Value;
        var y = Settings.PositionY.Value;

        var labelColor = Settings.TextColor.Value;

        var headerText = $"Kills: {totalKills}";
        var headerSize = Graphics.MeasureText(headerText);

        // Build rarity segments for detail line
        var segments = new List<(string Label, string Value, Color Color)>();

        if (Settings.ShowDetail.Value)
        {
            if (Settings.ShowWhite.Value)
                segments.Add(("White: ", _killsByRarity[MonsterRarity.White].ToString(), Settings.WhiteColor.Value));

            if (Settings.ShowMagic.Value)
                segments.Add(("Magic: ", _killsByRarity[MonsterRarity.Magic].ToString(), Settings.MagicColor.Value));

            if (Settings.ShowRare.Value)
                segments.Add(("Rare: ", _killsByRarity[MonsterRarity.Rare].ToString(), Settings.RareColor.Value));

            if (Settings.ShowUnique.Value)
                segments.Add(("Unique: ", _killsByRarity[MonsterRarity.Unique].ToString(), Settings.UniqueColor.Value));
        }

        // Measure detail line width
        float detailWidth = 0f;
        float detailLineHeight = 0f;
        var hasDetail = segments.Count > 0;

        if (hasDetail)
        {
            bool first = true;
            foreach (var seg in segments)
            {
                if (!first)
                {
                    var sepSize = Graphics.MeasureText(" / ");
                    detailWidth += sepSize.X;
                    if (sepSize.Y > detailLineHeight)
                        detailLineHeight = sepSize.Y;
                }

                var labelSize = Graphics.MeasureText(seg.Label);
                detailWidth += labelSize.X;
                if (labelSize.Y > detailLineHeight)
                    detailLineHeight = labelSize.Y;

                var valueSize = Graphics.MeasureText(seg.Value);
                detailWidth += valueSize.X;
                if (valueSize.Y > detailLineHeight)
                    detailLineHeight = valueSize.Y;

                first = false;
            }
        }

        var maxWidth = headerSize.X;
        if (hasDetail && detailWidth > maxWidth)
            maxWidth = detailWidth;

        var lineHeight = Math.Max(headerSize.Y, detailLineHeight);
        var lineCount = 1 + (hasDetail ? 1 : 0);

        // Build strings for background utility
        var linesForBackground = new List<string> { headerText };
        if (hasDetail && Settings.ShowDetail.Value)
        {
            // This is only for approximate background sizing
            var detailText = BuildDetailLineForBackground(segments);
            linesForBackground.Add(detailText);
        }

        DrawBackground(x, y, linesForBackground, maxWidth, lineHeight, lineCount);

        // Actual drawing
        var pos = new Vector2(x, y);

        var drawHeaderSize = Graphics.DrawText(headerText, pos, labelColor, FontAlign.Left);
        pos.Y += drawHeaderSize.Y;

        if (!hasDetail || !Settings.ShowDetail.Value)
            return;

        pos.Y += 2;

        var detailPos = pos;
        bool firstDraw = true;

        foreach (var seg in segments)
        {
            if (!firstDraw)
            {
                var sepSize = Graphics.DrawText(" / ", detailPos, labelColor, FontAlign.Left);
                detailPos.X += sepSize.X;
            }

            var labelSize = Graphics.DrawText(seg.Label, detailPos, labelColor, FontAlign.Left);
            detailPos.X += labelSize.X;

            var valueSize = Graphics.DrawText(seg.Value, detailPos, seg.Color, FontAlign.Left);
            detailPos.X += valueSize.X;

            firstDraw = false;
        }
    }

    private static string BuildDetailLineForBackground(List<(string Label, string Value, Color Color)> segments)
    {
        if (segments.Count == 0)
            return string.Empty;

        var result = string.Empty;
        var first = true;
        foreach (var seg in segments)
        {
            if (!first)
                result += " / ";
            result += seg.Label + seg.Value;
            first = false;
        }

        return result;
    }

    #endregion

    #region Background

    private void DrawBackground(int x, int y, List<string> lines)
    {
        if (lines.Count == 0)
            return;

        float maxWidth = 0f;
        float lineHeight = 0f;

        foreach (var line in lines)
        {
            var size = Graphics.MeasureText(line);
            if (size.X > maxWidth)
                maxWidth = size.X;
            if (size.Y > lineHeight)
                lineHeight = size.Y;
        }

        var width = maxWidth + BackgroundPadding * 2;
        var height = lineHeight * lines.Count + BackgroundPadding * 2;

        var rect = new RectangleF(
            x - BackgroundPadding,
            y - BackgroundPadding,
            width,
            height);

        Graphics.DrawBox(rect, BackgroundColor);
    }

    private void DrawBackground(int x, int y, List<string> lines, float maxWidth, float lineHeight, int lineCount)
    {
        if (lineCount <= 0)
            return;

        if (maxWidth <= 0)
        {
            // Fallback to generic measurement if not provided.
            DrawBackground(x, y, lines);
            return;
        }

        var width = maxWidth + BackgroundPadding * 2;
        var height = lineHeight * lineCount + BackgroundPadding * 2;

        var rect = new RectangleF(
            x - BackgroundPadding,
            y - BackgroundPadding,
            width,
            height);

        Graphics.DrawBox(rect, BackgroundColor);
    }

    #endregion
}
