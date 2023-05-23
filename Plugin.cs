using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

using TerrariaApi.Server;
using TShockAPI;

using Newtonsoft.Json;

namespace GetItemFromTile
{
    [ApiVersion(2, 1)]
    public class GetItemFromTilePlugin : TerrariaPlugin
    {
        #region Base

        public override string Author => "Zoom L1 & few others i don't know";
        public override string Name => nameof(GetItemFromTile);
        public GetItemFromTilePlugin(Main game) : base(game) { }

        private Command stateCommand;

        #endregion

        #region Initialize

        public override void Initialize()
        {
            stateCommand = new Command("getitemfromtile", SetStateCommand, "tileitem", "tile");
            Commands.ChatCommands.Add(stateCommand);

            ServerApi.Hooks.NetGetData.Register(this, OnGetData); 
        }

        protected override void Dispose(bool disposed)
        {
            if (disposed)
            {
                Commands.ChatCommands.Remove(stateCommand);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            }
            base.Dispose();
        }

        #endregion

        #region Commands

        public void SetStateCommand(CommandArgs args)
        {
            bool value = !args.Player.TileInfoState();
            args.Player.TileInfoState(value);
            args.Player.SendInfoMessage("Value is " + value);
        }

        #endregion

        public void OnGetData(GetDataEventArgs args)
        {
            if (args.MsgID == PacketTypes.MassWireOperation)
            {
                var player = TShock.Players[args.Msg.whoAmI];

                if (!player.TileInfoState())
                    return;

                BinaryReader reader = args.Msg.reader;
                reader.BaseStream.Position = args.Index;

                int x = reader.ReadInt16();
                int y = reader.ReadInt16();

                ITile t = Main.tile[x, y];
                Item item = new Item();

                try
                {
                    item = GetItemFromTileV5(t); // only for furniture
                }
                catch { }

                var anonType = new
                {
                    Type = t.type,
                    
                    Active = t.active(),
                    InActive = t.inActive(),

                    Slope = t.slope(),
                    Color = t.color(),

                    Wall = t.wall, 
                    WallColor = t.wallColor(),

                    Liquid = new { Count = t.liquid, Type = t.liquidType(), 
                        Skipping = t.skipLiquid(), Checking = t.checkingLiquid() },
                    
                    Frames = new { X = t.frameX, Y = t.frameY },

                    Item = new { NetID = item?.netID, PlaceStyle = item?.placeStyle }
                };

                player.SendInfoMessage(JsonConvert.SerializeObject(anonType));
            }
        }

        [Obsolete("Doesn't return place style")]
        public Item GetItemFromTileV1(ITile tile)
        {
            return ContentSamples.ItemsByType.First(i => i.Value.createTile == tile.type).Value;
        }
        public Item GetItemFromTileV2(ITile tile, out TileObjectData tileData)
        {
            TileObjectData data = TileObjectData.GetTileData(tile);
            tileData = data;

            return ContentSamples.ItemsByType
                .First(i => i.Value.createTile == tile.type && i.Value.placeStyle == data.Style).Value;
        }
        public Item GetItemFromTileV3(ITile tile, int x, int y)
        {
            WorldGen.KillTile_GetItemDrops(x, y, tile, out int netid, out int stack, 
                out int secondItem, out int secondStack);
            Item item = new Item();
            item.netDefaults(netid);
            item.stack = stack;
            return item;
        }

        public Item GetItemFromTileV4(ITile t)
        {
            var list = ContentSamples.ItemsByType.Where(i => i.Value.createTile == t.type);
            TileObjectData data = TileObjectData._data[t.type];

            int x1 = (int)t.frameX / data.CoordinateFullWidth;
            int y1 = (int)t.frameY / data.CoordinateFullHeight;

            int wrap = data.StyleWrapLimit;
            if (wrap == 0)
                wrap = 1;
            int hor;
            if (data.StyleHorizontal)
                hor = y1 * wrap + x1;
            else
                hor = x1 * wrap + y1;

            int expectedStyle = hor / data.StyleMultiplier;

            int styleLineSkip = data.StyleLineSkip;
            if (styleLineSkip > 1)
            {
                if (data.StyleHorizontal)
                    expectedStyle = y1 / styleLineSkip * wrap + x1;
                else
                    expectedStyle = x1 / styleLineSkip * wrap + y1;
            }

            int extraTiles = 0;

            switch (t.type)
            {
                case TileID.ClosedDoor:
                    if (expectedStyle > 11)
                        extraTiles++;
                    break;
                case TileID.Containers:
                    if (expectedStyle > 2)
                        extraTiles++;
                    if (expectedStyle > 4)
                        extraTiles++;

                    if (expectedStyle > 27)
                        extraTiles += 5;
                    if (expectedStyle > 36)
                        extraTiles++;
                    if (expectedStyle > 38)
                        extraTiles++;
                    if (expectedStyle > 40)
                        extraTiles++;
                    break;
            }

            return list.ElementAt(expectedStyle - extraTiles).Value;
        }
        public Item GetItemFromTileV4(int x, int y)
        {
            return GetItemFromTileV4(Main.tile[x, y]);
        }

        public Item GetItemFromTileV5(ITile t)
        {
            return ContentSamples.ItemsByType
                .FirstOrDefault(i => i.Value.placeStyle == GetStyleFromTileV5(t, out int type) 
                    && i.Value.createTile == type).Value;
        }
        public int GetStyleFromTileV5(ITile t, out int type)
        {
            type = t.type;

            switch (type)
            {
                case TileID.OpenDoor:
                    type = TileID.ClosedDoor;
                    break;
            }    

            TileObjectData data = TileObjectData._data[type];

            int x1 = (int)t.frameX / data.CoordinateFullWidth;
            int y1 = (int)t.frameY / data.CoordinateFullHeight;

            int wrap = data.StyleWrapLimit;
            if (wrap == 0)
                wrap = 1;
            int hor;
            if (data.StyleHorizontal)
                hor = y1 * wrap + x1;
            else
                hor = x1 * wrap + y1;

            int expectedStyle = hor / data.StyleMultiplier;

            int styleLineSkip = data.StyleLineSkip;
            if (styleLineSkip > 1)
            {
                if (data.StyleHorizontal)
                    expectedStyle = y1 / styleLineSkip * wrap + x1;
                else
                    expectedStyle = x1 / styleLineSkip * wrap + y1;
            }

            return expectedStyle;
        }
    }

    public static class Extensions
    {
        public static bool TileInfoState(this TSPlayer player)
        {
            if (!player.ContainsData(nameof(GetItemFromTile)))
                return false;
            return player.GetData<bool>(nameof(GetItemFromTile));
        }
        public static void TileInfoState(this TSPlayer player, bool state)
        {
            player.SetData(nameof(GetItemFromTile), state);
        }
    }
}

