using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CactusPie.MapLocation.Minimap.Events;
using CactusPie.MapLocation.Minimap.Services.Interfaces;
using EFT.UI.Ragfair;

namespace CactusPie.MapLocation.Minimap.Services;

public sealed class MapDataReceiver : IMapDataReceiver
{
    private readonly UdpClient _udpClient;
    private IPEndPoint _receiveEndpoint;
    private CancellationTokenSource? _dataReceivingCancellationToken;

    public event EventHandler<MapPositionDataReceivedEventArgs>? MapPositionDataReceived;

    public MapDataReceiver(Func<UdpClient> udpClientFactory, IPEndPoint receiveEndpoint)
    {
        _udpClient = udpClientFactory();
        _receiveEndpoint = receiveEndpoint;
        _udpClient.Client.Bind(_receiveEndpoint);
    }

    [STAThread]
    public void StartReceivingData()
    {
        if (_dataReceivingCancellationToken != null)
        {
            throw new InvalidOperationException("The receive operation is already running");
        }
        
        Task.Run(() =>
        {
            _dataReceivingCancellationToken = new CancellationTokenSource();
            CancellationToken cancellationToken = _dataReceivingCancellationToken.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] receivedData;
                const int blockingCallInterruptedErrorCode = 10004;
                
                try
                {
                    receivedData = _udpClient.Receive(ref _receiveEndpoint);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var mapNameLength = BitConverter.ToInt32(receivedData, 0);
                int offset = sizeof(int);
                
                string mapName = Encoding.UTF8.GetString(receivedData, offset, mapNameLength);
                offset += mapNameLength;


                if (mapName == null)
                    return;

                if (mapName.StartsWith("_loot"))
                {
                    MessageBox.Show("Got _loot header!");
                    //HandleLooseLoot(ref receivedData,ref offset);

                    //Loose Loot Item Data
                    var _itemDataLength = BitConverter.ToInt32(receivedData, offset);
                    offset += sizeof(int);

                    string _itemDataByteString = Encoding.UTF8.GetString(receivedData, offset, _itemDataLength);
                    offset += _itemDataLength;

                    Clipboard.SetText(_itemDataByteString);
                }
                else
                {
                    var xPosition = BitConverter.ToSingle(receivedData, offset);
                    offset += sizeof(float);

                    var yPosition = BitConverter.ToSingle(receivedData, offset);
                    offset += sizeof(float);

                    var zPosition = BitConverter.ToSingle(receivedData, offset);
                    offset += sizeof(float);

                    var xRotation = BitConverter.ToSingle(receivedData, offset);
                    offset += sizeof(float);

                    var yRotation = BitConverter.ToSingle(receivedData, offset);

                    OnMapPositionDataReceived(new MapPositionDataReceivedEventArgs(mapName, xPosition, yPosition, zPosition, xRotation, yRotation));
                }              
            }
        });
    }

    public void HandleLooseLoot(ref byte[] receivedData, ref int offset)
    {
        //ID
        var _itemIdLength = BitConverter.ToInt32(receivedData, offset);
        offset += sizeof(int);

        string _itemIdByteString = Encoding.UTF8.GetString(receivedData, offset, _itemIdLength);
        offset += _itemIdLength;


        //Name
        var _itemNameLength = BitConverter.ToInt32(receivedData, offset);
        offset += sizeof(int);

        string _itemNameByteString = Encoding.UTF8.GetString(receivedData, offset, _itemNameLength);
        offset += _itemNameLength;

        //Display Name
        var _itemDisplayNameLength = BitConverter.ToInt32(receivedData, offset);
        offset += sizeof(int);

        string _itemDisplayNameByteString = Encoding.UTF8.GetString(receivedData, offset, _itemDisplayNameLength);
        offset += _itemDisplayNameLength;

        //Parse Item Name Bytes
        string _itemId = _itemIdByteString.Split(" ")[0];
        string _itemName = _itemNameByteString.Split(" ")[0];
        string _displayName = _itemDisplayNameByteString.Split(" ")[0];

        HandleGetLoot(_itemId, _itemName, _displayName);
    }

    public void HandleGetLoot(string _id, string _name, string _display)
    {
        MessageBox.Show($"ID: {_id} : Name: {_name}\nDisplay Name: {_display}");
    }

    public void StopReceivingData()
    {
        _dataReceivingCancellationToken?.Cancel();
    }

    private void OnMapPositionDataReceived(MapPositionDataReceivedEventArgs e)
    {
        EventHandler<MapPositionDataReceivedEventArgs>? handler = MapPositionDataReceived;
        handler?.Invoke(this, e);
    }

    public void Dispose()
    {
        _udpClient.Dispose();
        if (_dataReceivingCancellationToken != null)
        {
            _dataReceivingCancellationToken?.Cancel();
            _dataReceivingCancellationToken?.Dispose();
        }
    }

}