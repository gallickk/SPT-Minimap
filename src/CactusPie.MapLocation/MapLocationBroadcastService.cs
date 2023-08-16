using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace CactusPie.MapLocation
{
    public sealed class MapLocationBroadcastService : IDisposable
    {
        private readonly GamePlayerOwner _gamePlayerOwner;
        private readonly GameWorld _gameWorld;
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _sendEndpoint;
        private readonly Timer _timer;
        private Dictionary<string, Item> _sentItems = new Dictionary<string, Item>();
        private bool sentLooseLoot = false;
        public MapLocationBroadcastService(GamePlayerOwner gamePlayerOwner, GameWorld _world, IPEndPoint ipEndPoint)
        {
            _gamePlayerOwner = gamePlayerOwner;
            _gameWorld = _world;

            _udpClient = new UdpClient();
            _sendEndpoint = ipEndPoint;
            _timer = new Timer
            {
                AutoReset = true,
                Interval = 1000,
            };

            _timer.Elapsed += (sender, args) =>
            {
                try
                {
                    SendPlayerData();

                    if(!sentLooseLoot)
                        SendItemData();
                }
                catch (Exception e)
                {
                    MapLocationPlugin.MapLocationLogger.LogError($"Exception {e.GetType()} occured. Message: {e.Message}. StackTrace: {e.StackTrace}");
                }
            };
        }

        public void StartBroadcastingPosition(double interval = 1000)
        {
            _timer.Interval = interval;
            _timer.Start();
        }

        public void StopBroadcastingPosition()
        {
            _timer.Stop();
        }
        
        public void ChangeInterval(double interval)
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
                _timer.Interval = interval;
                _timer.Start();
            }
            else
            {
                _timer.Interval = interval;
            }
        }

        private void SendPlayerData()
        {
            string mapName = _gamePlayerOwner.Player.Location;
            Vector3 playerPosition = _gamePlayerOwner.Player.Position;
            Vector2 playerRotation = _gamePlayerOwner.Player.Rotation;

            byte[] mapNameBytes = Encoding.UTF8.GetBytes(mapName);
            
            IEnumerable<byte[]> GetArrays()
            {
                yield return BitConverter.GetBytes(mapNameBytes.Length);
                yield return mapNameBytes;
                yield return BitConverter.GetBytes(playerPosition.x);
                yield return BitConverter.GetBytes(playerPosition.y);
                yield return BitConverter.GetBytes(playerPosition.z);
                yield return BitConverter.GetBytes(playerRotation.x);
                yield return BitConverter.GetBytes(playerRotation.y);
            };

            byte[] content = GetArrays().SelectMany(x => x).ToArray();

            _udpClient.Send(content, content.Length, _sendEndpoint);
        }

        private void SendItemData()
        {
            //Get Loot
            //var loot = _gameWorld.LootItems;
            var loot = _gameWorld.AllLoot
            if (loot == null || loot.Count <= 0)
                return;

            string m = "_loot";
            byte[] mapNameBytes = Encoding.UTF8.GetBytes(m);


            string looseLoot = "";
            List<ItemData> looseLootList = new List<ItemData>();
            for (int i = 0; i < loot.Count; i++)
            {
                LootItem lootItem = loot.GetByIndex(i) as LootItem;
                Item item = lootItem.Item;

                ItemData id = new ItemData() {
                    NetworkId = lootItem.GetNetId().ToString(),
                    ItemId = item.Id,
                    ItemName = item.ShortName.Localized(),
                    Position = lootItem.transform.position
                };

                looseLootList.Add(id);
                /*if (_sentItems.ContainsKey(lootItem.GetNetId().ToString()))
                    return;

                byte[] lootItemId = Encoding.UTF8.GetBytes(item.Id);
                byte[] lootItemName = Encoding.UTF8.GetBytes(item.ShortName.Localized());
                byte[] lootItemDisplayName = new byte[1024];

                if(item.Attributes.Count > 0)
                    lootItemDisplayName = Encoding.UTF8.GetBytes(item.Attributes[0].DisplayName);

                IEnumerable<byte[]> GetArrays()
                {
                    yield return BitConverter.GetBytes(mapNameBytes.Length);
                    yield return mapNameBytes;
                    yield return BitConverter.GetBytes(lootItemId.Length);
                    yield return lootItemId;
                    yield return BitConverter.GetBytes(lootItemName.Length);
                    yield return lootItemName;
                    yield return BitConverter.GetBytes(lootItemDisplayName.Length);
                    yield return lootItemDisplayName;
                };

                byte[] content = GetArrays().SelectMany(x => x).ToArray();

                _sentItems.Add(lootItem.GetNetId().ToString(), item);
                _udpClient.Send(content, content.Length, _sendEndpoint);*/
            }

            string looseLootJson = JsonConvert.SerializeObject(looseLootList);

            byte[] looseLootBytes = Encoding.UTF8.GetBytes(looseLootJson);

            IEnumerable<byte[]> GetArrays()
            {
                yield return BitConverter.GetBytes(mapNameBytes.Length);
                yield return mapNameBytes;
                yield return BitConverter.GetBytes(looseLootBytes.Length);
                yield return looseLootBytes;
            };

            byte[] content = GetArrays().SelectMany(x => x).ToArray();
            _udpClient.Send(content, content.Length, _sendEndpoint);
            sentLooseLoot = true;
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
            _timer?.Dispose();
        }
    }
}

struct ItemData
{
    public string NetworkId;
    public string ItemId;
    public string ItemName;
    public Vector3 Position;
}