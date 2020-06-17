//---------------------------------------------------------------------------------
// Copyright (c) June 2020, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------
namespace devMobile.IoT.Netduino.FieldGateway
{
   using System;
   using System.Diagnostics;
   using System.Text;
   using System.Threading;

   using Windows.Devices.Gpio;

   using devMobile.IoT.Rfm9x;
   using devMobile.NetNF.Sensor;
   

   class NetduinoClient
   {
      private const double Frequency = 915000000.0;
      private const string SpiBusId = "SPI2";
      private const string I2cBusId = "I2C1";
      private readonly byte[] fieldGatewayAddress = Encoding.UTF8.GetBytes("LoRaIoT1");
      private readonly byte[] deviceAddress = Encoding.UTF8.GetBytes("N3W");

      private readonly GpioPin led;
      private readonly Rfm9XDevice rfm9XDevice;
      private readonly TimeSpan dueTime = new TimeSpan(0, 0, 15);
      private readonly TimeSpan periodTime = new TimeSpan(0, 0, 300);
      private readonly SiliconLabsSI7005 sensor;

      public NetduinoClient()
      {
         Debug.WriteLine("devMobile.IoT.Rfm9x.ShieldSerial starting");

         led = GpioController.GetDefault().OpenPin(PinNumber('A', 10));
         led.SetDriveMode(GpioPinDriveMode.Output);

         // Arduino D10->PB10
         int chipSelectPinNumber = PinNumber('B', 10);
         // Arduino D9->PE5
         int resetPinNumber = PinNumber('E', 5);
         // Arduino D2->PA3
         int interruptPinNumber = PinNumber('A', 3);

         sensor = new SiliconLabsSI7005(I2cBusId);

         rfm9XDevice = new Rfm9XDevice(SpiBusId, chipSelectPinNumber, resetPinNumber, interruptPinNumber);
      }

      public void Run()
      {
         rfm9XDevice.Initialise(frequency: Frequency, paBoost: true, rxPayloadCrcOn: true);
         rfm9XDevice.Receive(deviceAddress);
         rfm9XDevice.OnReceive += Rfm9XDevice_OnReceive;
         rfm9XDevice.OnTransmit += Rfm9XDevice_OnTransmit;

         Timer humidityAndtemperatureUpdates = new Timer(HumidityAndTemperatureTimerProc, null, dueTime, periodTime);

         Thread.Sleep(Timeout.Infinite);
      }

      private void HumidityAndTemperatureTimerProc(object state)
      {
         double humidity = sensor.Humidity();
         double temperature = sensor.Temperature();

         Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} H:{humidity} T:{temperature}");

         rfm9XDevice.Send(fieldGatewayAddress, Encoding.UTF8.GetBytes("t " + temperature.ToString("F1") + ",H " + humidity.ToString("F0")));

         led.Write( GpioPinValue.High);
      }

      private void Rfm9XDevice_OnTransmit(object sender, Rfm9XDevice.OnDataTransmitedEventArgs e)
      {
         Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss}-TX Done");
         led.Write( GpioPinValue.Low);
      }

      private void Rfm9XDevice_OnReceive(object sender, Rfm9XDevice.OnDataReceivedEventArgs e)
      {
         try
         {
            string messageText = new string(UTF8Encoding.UTF8.GetChars(e.Data));
            string addressText = new string(UTF8Encoding.UTF8.GetChars(e.Address));

            Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss}-Rfm9X PacketSnr {e.PacketSnr} Packet RSSI {e.PacketRssi} dBm RSSI {e.Rssi}dBm = {e.Data.Length} byte message {messageText}");
         }
         catch (Exception ex)
         {
            Debug.WriteLine(ex.Message);
         }
      }

      static int PinNumber(char port, byte pin)
      {
         if (port < 'A' || port > 'J')
            throw new ArgumentException();

         return ((port - 'A') * 16) + pin;
      }
   }
}