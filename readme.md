# ACR Reactive BluetoothLE Plugin for Xamarin & Windows
Easy to use, cross platform, REACTIVE BluetoothLE Plugin for Xamarin (Windows UWP COMING SOON)

## PLATFORMS

* Android 4.3+
* iOS 6+
* Windows UWP (COMING SOON)

## SETUP

### Android

    Add the following to your AndroidManifest.xml
    <uses-permission android:name="android.permission.BLUETOOTH"/>
    <uses-permission android:name="android.permission.BLUETOOTH_ADMIN"/>

### iOS

    If you want to use background BLE periperhals, add the following to your Info.plist
    <array>
	<string>bluetooth-central</string>

### Windows

    Add to your app manifest file
    <Capabilities>
        <Capability Name="internetClient" />
        <DeviceCapability Name="bluetooth" />
    </Capabilities>

## HOW TO USE

### Scan for Devices

    var scanner = BleService.Adapter.Scan().Subscribe(scanResult => 
    {
        // do something with it
        // the scanresult contains the device, RSSI, and advertisement packet
        
    });

    scanner.Dispose(); // to stop scanning

### Connect/Disconnect to a device

    // connect
    await device.Connect(); // this is an observable so you can do other funky timeouts

    device.Disconnect();


### Monitor device states

    // This will tell you if the device name changes during a connection
    device.WhenNameChanged().Subscribe(string => {});

    // this will monitor the RSSI of the connected device
    device.WhenRssiChanged().Subscribe(rssi => {});

    // this will watch the connection states to the device
    device.WhenStatusChanged().Subscribe(connectionState => {});


### Smart/Persistent Connection

    var connection = device.PersistentConnect().Subscribe(connectionState => 
    {
        // you can see the connection transitions here
        // dont try to manage reconnections, this guy will do it for you!
    });

    connection.Dispose(); // this will close the connection and stop reconnection attempts.  The GC can also get at this for you!


### Discover services on a device

    // once you have successfully scanned for a device, use the instance to discover services
    // NOTE: you can call this repeatedly during the connection lifecycle to see all of the discovered services
    Device.WhenServicesDiscovered().Subscribe(service => 
    {
    });



### Discover characteristics on service

    Service.WhenCharacteristicDiscovered().Subscribe(characteristic => {});

### Read and Write to a characteristic

    // once you have your characteristic instance from the service discovery
    await Characteristic.Read();


### Register for Notifications on a characteristic

    // once you have your characteristic instance from the service discovery
    

### Discover descriptors on a characteristic


## FAQ

Q. Why is everything reactive instead of events/async
A. I wanted event streams as I was scanning devices.  I also wanted to throttle things like characteristic notification feeds.  Lastly, was the proper cleanup of events and resources.   

