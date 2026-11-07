using System;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Energy;
using WorldTree.Core.Items;

namespace WorldTree.Tests.Support
{
    /// <summary>Helpers for building sessions on hand-made boards.</summary>
    public static class TestContent
    {
        public static readonly Catalog Catalog = PrototypeContent.BuildCatalog();

        public static GameSession EmptySession(FakeClock clock, int width = 7, int height = 9, uint seed = 12345)
        {
            var board = new BoardModel(width, height);
            return new GameSession(Catalog, board, EnergyModel.Full(EnergyConfig.Default), seed, clock);
        }

        public static ItemInstance Item(string id)
        {
            var def = Catalog.GetItem(id);
            return new ItemInstance(id, def.IsGenerator ? Core.Generators.GeneratorRules.CreateFull(def.Generator) : null);
        }

        public static void Put(GameSession session, int x, int y, string id) => session.Board.SetItem(new GridPos(x, y), Item(id));

        public static void FillBoard(GameSession session, string id)
        {
            for (int y = 0; y < session.Board.Height; y++)
                for (int x = 0; x < session.Board.Width; x++)
                    if (session.Board.GetItem(new GridPos(x, y)) == null) Put(session, x, y, id);
        }

        public static GridPos P(int x, int y) => new GridPos(x, y);

        public static TimeSpan Minutes(double m) => TimeSpan.FromMinutes(m);
    }
}
