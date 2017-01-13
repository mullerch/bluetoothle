﻿using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;


namespace Acr.Ble
{
    public class Device : IDevice
    {
        readonly Subject<bool> deviceSubject;
        readonly IAdapter adapter;
        readonly DeviceInformation deviceInfo;
        BluetoothLEDevice native;


        public Device(IAdapter adapter, DeviceInformation deviceInfo)
        {
            this.adapter = adapter;
            this.deviceInfo = deviceInfo;
            this.deviceSubject = new Subject<bool>();
        }


        public string Name => this.deviceInfo.Name;
        public Guid Uuid { get; }


        public IGattReliableWriteTransaction BeginReliableWriteTransaction()
        {
            return new GattReliableWriteTransaction();
        }


        public IObservable<object> Connect(GattConnectionConfig config)
        {
            config = config ?? GattConnectionConfig.DefaultConfiguration;
            // TODO: config auto reconnect?
            // TODO: monitor devicewatcher - if removed d/c, if added AND paired - connected
            return Observable.Create<object>(async ob =>
            {
                if (this.Status == ConnectionStatus.Connected)
                {
                    ob.Respond(null);
                }
                else
                {
                    // TODO: connecting
                    this.native = await BluetoothLEDevice.FromIdAsync(this.deviceInfo.Id);

                    if (this.native == null)
                        throw new ArgumentException("Device Not Found");

                    // TODO: auto pairing config?
                    if (this.native.DeviceInformation.Pairing.CanPair && !this.native.DeviceInformation.Pairing.IsPaired)
                    {
                        var dpr = await this.native.DeviceInformation.Pairing.PairAsync(DevicePairingProtectionLevel.None);
                        if (dpr.Status != DevicePairingResultStatus.Paired)
                            throw new ArgumentException($"Pairing to device failed - " + dpr.Status);
                    }
                    ob.Respond(null);
                    this.deviceSubject.OnNext(true);
                }
                return Disposable.Empty;
            });
        }


        public IObservable<int> WhenRssiUpdated(TimeSpan? frequency = null)
        {
            // TODO: what if scan filters are applied?
            // TODO: create another advertisewatcher
            return this.adapter
                .Scan() // TODO: this will run a duplicate
                .Where(x => x.Device.Uuid.Equals(this.Uuid))
                .Select(x => x.Rssi);
        }


        public void CancelConnection()
        {
            if (this.Status != ConnectionStatus.Connected)
                return;

            this.native?.Dispose();
            this.native = null;
        }


        public ConnectionStatus Status
        {
            get
            {
                // TODO: monitor devicewatcher - if removed d/c, if added AND paired - connected
                if (this.native == null)
                    return ConnectionStatus.Disconnected;

                switch (this.native.ConnectionStatus)
                {
                    case BluetoothConnectionStatus.Connected:
                        return ConnectionStatus.Connected;

                    default:
                        return ConnectionStatus.Disconnected;
                }
            }
        }


        IObservable<ConnectionStatus> statusOb;
        public IObservable<ConnectionStatus> WhenStatusChanged()
        {
            // TODO: monitor devicewatcher - if removed d/c, if added AND paired - connected
            // TODO: shut devicewatcher off if characteristic hooked?
            this.statusOb = this.statusOb ?? Observable.Create<ConnectionStatus>(ob =>
            {
                ob.OnNext(this.Status);
                var handler = new TypedEventHandler<BluetoothLEDevice, object>(
                    (sender, args) => ob.OnNext(this.Status)
                );

                var sub = this.deviceSubject
                    .AsObservable()
                    .Subscribe(x =>
                    {
                        ob.OnNext(this.Status);
                        if (this.native != null)
                            this.native.ConnectionStatusChanged += handler;
                    });

                return () =>
                {
                    sub.Dispose();
                    if (this.native != null)
                        this.native.ConnectionStatusChanged -= handler;
                };
            })
            .Replay(1);

            return this.statusOb;
        }


        IObservable<IGattService> serviceOb;
        public IObservable<IGattService> WhenServiceDiscovered()
        {
            this.serviceOb = this.serviceOb ?? Observable.Create<IGattService>(ob =>
                this
                    .WhenStatusChanged()
                    .Where(x => x == ConnectionStatus.Connected)
                    .Subscribe(x =>
                    {
                        foreach (var nservice in this.native.GattServices)
                        {
                            var service = new GattService(nservice, this);
                            ob.OnNext(service);
                        }
                    })
            )
            .ReplayWithReset(this.WhenStatusChanged()
                .Skip(1)
                .Where(x => x == ConnectionStatus.Disconnected)
            )
            .RefCount();

            return this.serviceOb;
        }


        IObservable<string> nameOb;
        public IObservable<string> WhenNameUpdated()
        {
            this.nameOb = this.nameOb ?? Observable.Create<string>(ob =>
            {
                ob.OnNext(this.Name);
                var handler = new TypedEventHandler<BluetoothLEDevice, object>(
                    (sender, args) => ob.OnNext(this.Name)
                );
                var sub = this.WhenStatusChanged()
                    .Where(x => x == ConnectionStatus.Connected)
                    .Subscribe(x => this.native.NameChanged += handler);

                return () =>
                {
                    sub.Dispose();
                    if (this.native != null)
                        this.native.NameChanged -= handler;
                };
            })
            .Publish()
            .RefCount();

            return this.nameOb;
        }


        public IObservable<IGattService> FindServices(params Guid[] serviceId)
        {
            throw new NotImplementedException();
        }


        public PairingStatus PairingStatus => this.native.DeviceInformation.Pairing.IsPaired
            ? PairingStatus.Paired
            : PairingStatus.NotPaired;


        public bool IsPairingRequestSupported => true;
        public IObservable<bool> PairingRequest(string pin = null)
        {
            return Observable.Create<bool>(async ob =>
            {
                var result = await this.native.DeviceInformation.Pairing.PairAsync(DevicePairingProtectionLevel.None);
                var status = result.Status == DevicePairingResultStatus.Paired;
                ob.Respond(status);
                return Disposable.Empty;
            });
        }

        public bool IsMtuRequestAvailable => false;
        public IObservable<int> RequestMtu(int size)
        {
            throw new NotImplementedException();
        }


        public int GetCurrentMtuSize()
        {
            return 20;
        }

        public IObservable<int> WhenMtuChanged()
        {
            return Observable.Return(this.GetCurrentMtuSize());
        }


        string ToMacAddress(ulong address)
        {
            var tempMac = address.ToString("X");
            //tempMac is now 'E7A1F7842F17'

            //string.Join(":", BitConverter.GetBytes(BluetoothAddress).Reverse().Select(b => b.ToString("X2"))).Substring(6);
            var regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            var replace = "$1:$2:$3:$4:$5:$6";
            var macAddress = Regex.Replace(tempMac, regex, replace);
            return macAddress;
        }


        Guid ToDeviceId(string address)
        {
            var deviceGuid = new byte[16];
            var mac = address.Replace(":", "");
            var macBytes = Enumerable
                .Range(0, mac.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(mac.Substring(x, 2), 16))
                .ToArray();

            macBytes.CopyTo(deviceGuid, 10);
            return new Guid(deviceGuid);
        }
    }
}