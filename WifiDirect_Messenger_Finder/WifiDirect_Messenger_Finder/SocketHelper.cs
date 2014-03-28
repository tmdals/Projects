using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace WifiDirect_Messenger_Finder
{
    public class BufferConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            String metadata = String.Empty;
            IBuffer buffer = value as IBuffer;
            if (buffer != null)
            {
                using (var metadataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                {
                    metadata = metadataReader.ReadString(buffer.Length);
                }
                metadata = String.Format("({0})", metadata);
            }
            return metadata;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class ConnectedPeer
    {
        public StreamSocket _socket;
        public bool _socketClosed;
        public DataWriter _dataWriter;
        public ConnectedPeer(StreamSocket socket, bool socketClosed, DataWriter dataWriter)
        {
            _socket = socket;
            _socketClosed = socketClosed;
            _dataWriter = dataWriter;
        }
    }
    public class SocketEventArgs : EventArgs
    {
        public SocketEventArgs(string s)
        {
            msg = s;
        }
        private string msg;
        public string Message
        {
            get { return msg; }
        }
    }
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string s)
        {
            msg = s;
        }
        private string msg;
        public string Message
        {
            get { return msg; }
        }
    }
    public enum ConnectState
    {
        PeerFound,
        Listening,
        Connecting,
        Completed,
        Canceled,
        Failed
    };
    class SocketHelper
    {
        List<ConnectedPeer> _connectedPeers = new List<ConnectedPeer>();

        public event EventHandler<SocketEventArgs> RaiseSocketErrorEvent;
        public event EventHandler<MessageEventArgs> RaiseMessageEvent;
        public ReadOnlyCollection<ConnectedPeer> ConnectedPeers
        {
            get { return new ReadOnlyCollection<ConnectedPeer>(_connectedPeers); }
        }
        public void Add(ConnectedPeer p)
        {
            _connectedPeers.Add(p);
        }
        public async void SendMessageToPeer(String message, ConnectedPeer connectedPeer)
        {
            try
            {
                if (!connectedPeer._socketClosed)
                {
                    DataWriter dataWriter = connectedPeer._dataWriter;

                    uint msgLength = dataWriter.MeasureString(message);
                    dataWriter.WriteInt32((int)msgLength);
                    dataWriter.WriteString(message);

                    uint numBytesWritten = await dataWriter.StoreAsync();
                    if (numBytesWritten > 0)
                    {
                        OnRaiseMessageEvent(new MessageEventArgs("Sent message: " + message + ", number of bytes written: " + numBytesWritten));
                    }
                    else
                    {
                        OnRaiseSocketErrorEvent(new SocketEventArgs("The remote side closed the socket"));
                    }
                }
            }
            catch (Exception err)
            {
                if (!connectedPeer._socketClosed)
                {
                    OnRaiseSocketErrorEvent(new SocketEventArgs("Failed to send message with error: " + err.Message));
                }
            }
        }
        public async void StartReader(ConnectedPeer connectedPeer)
        {
            try
            {
                using (var socketReader = new Windows.Storage.Streams.DataReader(connectedPeer._socket.InputStream))
                {
                    uint bytesRead = await socketReader.LoadAsync(sizeof(uint));
                    if (bytesRead > 0)
                    {
                        uint strLength = (uint)socketReader.ReadUInt32();
                        bytesRead = await socketReader.LoadAsync(strLength);
                        if (bytesRead > 0)
                        {
                            String message = socketReader.ReadString(strLength);
                            OnRaiseMessageEvent(new MessageEventArgs("Got message: " + message));
                            StartReader(connectedPeer);
                        }
                        else
                        {
                            OnRaiseSocketErrorEvent(new SocketEventArgs("The remote side closed the socket"));
                        }

                        socketReader.DetachStream();
                    }
                }
            }
            catch (Exception e)
            {
                if (!connectedPeer._socketClosed)
                {
                    OnRaiseSocketErrorEvent(new SocketEventArgs("Reading from socket failed: " + e.Message));
                }
            }
        }
        public void CloseSocket()
        {
            foreach (ConnectedPeer obj in _connectedPeers)
            {
                if (obj._socket != null)
                {
                    obj._socketClosed = true;
                    obj._socket.Dispose();
                    obj._socket = null;
                }

                if (obj._dataWriter != null)
                {
                    obj._dataWriter.Dispose();
                    obj._dataWriter = null;
                }
            }

            _connectedPeers.Clear();
        }
        protected virtual void OnRaiseSocketErrorEvent(SocketEventArgs e)
        {
            EventHandler<SocketEventArgs> handler = RaiseSocketErrorEvent;

            if (handler != null)
            {
                handler(this, e);
            }
        }
        protected virtual void OnRaiseMessageEvent(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = RaiseMessageEvent;

            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
