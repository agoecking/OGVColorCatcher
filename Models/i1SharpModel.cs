using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGVColorCatcher.Models
{
    public class I1SharpModel
    {
        /// <summary>
        /// Collection containing the connected devices
        /// </summary>
        public ObservableCollection<I1Pro64> Devices { get; private set; }

        public delegate void DeviceChangedHandler(I1Pro64 device);
        /// <summary>
        /// Occurs when the active device changes  
        /// </summary>
        public event DeviceChangedHandler DeviceChanged;

        private I1Pro64 currentDevice;
        /// <summary>
        /// Get/Set the active device.
        /// </summary>
        //public I1Pro64 CurrentDevice
        //{
        //    get { return currentDevice; }
        //    set
        //    {
        //        if (currentDevice != null)
        //        {
        //            currentDevice.Dispose();
        //        }
        //        currentDevice = null;
        //        try
        //        {
        //            I1Pro64 newDevice = value;
        //            newDevice.Open();
        //            currentDevice = newDevice;
        //        }
        //        catch (Exception)
        //        {
        //        }

        //        if (DeviceChanged != null)
        //        {
        //            DeviceChanged(currentDevice);
        //        }
        //    }
        //}

        public I1Pro64 CurrentDevice
        {
            get { return currentDevice; }
            set
            {
                if (ReferenceEquals(currentDevice, value)) return;

                if (currentDevice != null)
                {
                    currentDevice.Dispose();
                }
                currentDevice = null;

                if (value != null)
                {
                    try
                    {
                        value.Open();
                        currentDevice = value;
                    }
                    catch (Exception)
                    {
                        currentDevice = null;
                    }
                }

                // ✅ Só dispara o evento se for diferente de null
                if (currentDevice != null)
                {
                    DeviceChanged?.Invoke(currentDevice);
                }
            }
        }



        public BackgroundWorker worker;

        /// <summary>
        /// Constructor of the model
        /// </summary>
        public I1SharpModel()
        {
            Devices = new ObservableCollection<I1Pro64>();

            I1Pro64.DeviceConnected += I1Pro64_DeviceConnected;
            I1Pro64.DeviceDisconnected += I1Pro64_DeviceDisconnected;
        }

        /// <summary>
        /// Event handler for "device connected" events
        /// </summary>
        /// <param name="device">The connected device</param>
        private void I1Pro64_DeviceConnected(I1Pro64 device)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Devices.Add(device);
            }
            );
        }

        /// <summary>
        /// Event handler for "device disconnected" events
        /// </summary>
        /// <param name="device">The disconnected device</param>
        private void I1Pro64_DeviceDisconnected(I1Pro64 device)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Devices.Remove(device);
            }
            );
        }

        /// <summary>
        /// Start a device search action. Devices is changed after the action finished
        /// </summary>
        public void SearchDevices()
        {
            worker = new BackgroundWorker();
            worker.DoWork += worker_DoWork;
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Device search action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var device in I1Pro64.LoadDevices())
                {
                    Devices.Add(device);
                }
            }
            );
        }
    }
}
