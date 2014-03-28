using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WifiDirect_HelloWorld
{
    public sealed partial class MainPage : Page
    {
        private IReadOnlyList<PeerInformation> _peerInformationList;
        private PeerInformation _requestingPeer;
        private bool _triggeredConnectSupported = false;
        private bool _browserConnectSupported = false;
        private bool _launchByTap = false;
        private SocketHelper _socketHelper = new SocketHelper();
        private string _discoveryData = "Hello";

        int button_function = 0;
        public MainPage()
        {
            this.InitializeComponent();

            _socketHelper.RaiseSocketErrorEvent += SocketErrorHandler;
            _socketHelper.RaiseMessageEvent += MessageHandler;

            _triggeredConnectSupported = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Triggered) == PeerDiscoveryTypes.Triggered;
            _browserConnectSupported = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) == PeerDiscoveryTypes.Browse;

        }
        async private void TriggeredConnectionStateChangedEventHandler(object sender, TriggeredConnectionStateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == TriggeredConnectState.PeerFound)
            {
                textBlock1.Text = "Socket connection starting!";
            }

            if (eventArgs.State == TriggeredConnectState.Completed)
            {
                textBlock1.Text = "Socket connect success!";
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        this.PeerFinder_StartSendReceive(eventArgs.Socket, null);
                    });
            }

            if (eventArgs.State == TriggeredConnectState.Failed)
            {
                textBlock1.Text = "Socket connect failed!";
            }
        }

        private bool _peerFinderStarted = false;
        private void SocketErrorHandler(object sender, SocketEventArgs e)
        {
            textBlock1.Text = e.Message;

            _socketHelper.CloseSocket();
        }
        private void MessageHandler(object sender, MessageEventArgs e)
        {
            textBox1.Text += e.Message + "\n";
        }
        private void PeerConnectionRequested(object sender, ConnectionRequestedEventArgs args)
        {
            _requestingPeer = args.PeerInformation;
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    textBlock1.Text = "Connection requested from peer " + args.PeerInformation.DisplayName;
                });
        }
        private void OnKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                String message = textBox2.Text;
                textBox2.Text = "";
                if (message.Length > 0)
                {
                    foreach (ConnectedPeer obj in _socketHelper.ConnectedPeers)
                    {
                        _socketHelper.SendMessageToPeer(message, obj);
                    }
                }
                else
                {
                    textBlock1.Text = "Please type a message";
                }
            }
        }
        void PeerFinder_StartSendReceive(StreamSocket socket, PeerInformation peerInformation)
        {
            ConnectedPeer connectedPeer = new ConnectedPeer(socket, false, new Windows.Storage.Streams.DataWriter(socket.OutputStream));
            _socketHelper.Add(connectedPeer);

            if (!_peerFinderStarted)
            {
                _socketHelper.CloseSocket();
                return;
            }

            if (peerInformation != null)
            {
                textBlock1.Text = peerInformation.DisplayName;
            }

            _socketHelper.StartReader(connectedPeer);
        }
        async void Button_Click(object sender, RoutedEventArgs e)
        {
            switch (button_function)
            {
                case 0:
                    {
                        PeerFinder.TriggeredConnectionStateChanged += new TypedEventHandler<object,TriggeredConnectionStateChangedEventArgs>(TriggeredConnectionStateChangedEventHandler);
                        PeerFinder.ConnectionRequested += new TypedEventHandler<object,ConnectionRequestedEventArgs>(PeerConnectionRequested);
                        PeerFinder.Role = PeerRole.Peer;
                        if (_discoveryData.Length > 0)
                        {
                            using (var discoveryDataWriter = new Windows.Storage.Streams.DataWriter(new Windows.Storage.Streams.InMemoryRandomAccessStream()))
                            {
                                discoveryDataWriter.WriteString(_discoveryData);
                                PeerFinder.DiscoveryData = discoveryDataWriter.DetachBuffer();
                            }
                        }
                        PeerFinder.Start();
                        _peerFinderStarted = true;
                        button1.Content = "Wait...";
                        textBlock1.Text = "Searching...";
                        //await Task.Delay(1000);
                        try
                        {
                            _peerInformationList = await PeerFinder.FindAllPeersAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("FindAllPeersAsync throws exception" + ex.Message);
                        }
                        Debug.WriteLine("Async operation completed");
                        if ((_peerInformationList != null) && (_peerInformationList.Count > 0))
                        {
                            textBlock1.Text = _peerInformationList[0].DisplayName;
                            button1.Content = "Connect";
                            button_function = 1;
                        }
                        else
                        {
                            textBlock1.Text = "None Found";
                            button_function = 0;
                            button1.Content = "Try again";
                            await Task.Delay(2000);
                            PeerFinder.Stop();
                            button1.Content = "Find";
                            textBlock1.Text = "Standby";
                        }

                        break;
                    }
                case 1:
                    {
                        PeerInformation peerToConnect = null;
                        try
                        {
                            button_function = 2;
                            button1.Content = "Wait...";
                            textBlock1.Text = "Connecting to" + _peerInformationList[0].DisplayName + "...";
                            peerToConnect = (PeerInformation)_peerInformationList[0];
                            StreamSocket socket = await PeerFinder.ConnectAsync(peerToConnect);
                            button1.Content = "Disconnect";
                            textBlock1.Text = "Connection succedded";
                            PeerFinder_StartSendReceive(socket, peerToConnect);
                        }
                        catch (Exception)
                        {
                            textBlock1.Text = "Connection to " + _peerInformationList[0].DisplayName + " failed";
                            button_function = 0;
                            button1.Content = "Try again";
                        }
                        if (button_function == 0)
                        {
                            PeerFinder.Stop();
                            await Task.Delay(2000);
                            button1.Content = "Find";
                            textBlock1.Text = "Standby";
                        }
                        break;
                    }
                case 2:
                    {
                        button_function = 0;
                        button1.Content = "Goodbye...";
                        textBlock1.Text = "Disconnecting...";
                        PeerFinder.Stop();
                        await Task.Delay(2000);
                        button1.Content = "Find";
                        textBlock1.Text = "Standby";
                        break;
                    }
                default:
                    break;
            }
        }
    }
}
