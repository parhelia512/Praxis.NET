﻿using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Praxis.Core;

namespace PlatformerSample;

[ExecuteAfter(typeof(BasicForwardRenderer))]
public class HudSystem : PraxisSystem
{
    public override SystemExecutionStage ExecutionStage => SystemExecutionStage.Draw;

    private Canvas _uiCanvas;
    private UIRenderer _uiRenderer;

    private TextWidget _score;

    public HudSystem(WorldContext context) : base(context)
    {
        _uiCanvas = new Canvas(Game);
        
        _score = new TextWidget
        {
            id = "score",
            anchorMin = new Vector2(0.5f, 0f),
            anchorMax = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 8f),
            sizeDelta = new Vector2(256f, 32f),
            pivot = new Vector2(0.5f, 0f),
            wordWrap = false,
            font = Game.Resources.Load<Font>("content/font/RussoOne-Regular.ttf"),
            Text = "/esScore: /c[yellow]0",
            size = 32,
            alignment = TextHorizontalAlignment.Center
        };
        _uiCanvas.AddWidget(_score);
        _uiRenderer = new UIRenderer(Game.GraphicsDevice);
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        foreach (var _ in World.GetMessages<PickupMessage>())
        {
            var stats = World.GetSingleton<PlayerStats>();
            _score.Text = $"/esScore: /c[yellow]{stats.score}";
        }

        _uiCanvas.UpdateLayout(_uiRenderer);
        _uiCanvas.DrawUI(_uiRenderer);
    }
}