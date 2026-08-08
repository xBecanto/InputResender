using FluentAssertions;
using MdxLibs.Services.NetClientService;
using MdxLibs.Services.NetClientService.InMemNet;
using Xunit;

namespace MdxLibs.ServiceTests.NetServices;
public class NetworkConnectionHappyFlowTest {
    const int PacketSize = NetConnectionGroup.PacketSize;

    public static INetPoint[] GetNetPoints ( string type, int cnt ) {
        INetPoint[] ret;
        switch ( type ) {
        case nameof ( InMemNetPoint ): ret = INetPoint.NextAvailable<InMemNetPoint> ( cnt, 1000 ); break;
        case nameof ( IPNetPoint ): ret = INetPoint.NextAvailable<IPNetPoint> ( cnt, 1000 ); break;
        default: throw new System.ArgumentException ( $"Type {type} is not supported" );
        }

        ret.Should ().NotBeNull ().And.HaveCount ( cnt );

        for ( int i = 0; i < cnt; i++ ) {
            ret[i].Should ().NotBeNull ();
            ret[i].DscName.Should ().BeNullOrWhiteSpace ();

            for ( int j = 0; j < i; j++ ) {
                ret[i].Should ().NotBeSameAs ( ret[j] ).And.NotBe ( ret[j] );
                ret[i].FullNetworkPath.Should ().NotBe ( ret[j].FullNetworkPath );
            }
        }
        return ret;
    }

    [Theory]
    [InlineData ( nameof ( InMemNetPoint ) )]
    [InlineData ( nameof ( IPNetPoint ) )]
    public void ConnectSendClose ( string type ) {
        var Conn = new BidirectionalConnection ( type );
        Conn.Connect ();

        Conn.SendTo ( 0 );
        Conn.SendTo ( 1 );

        Conn.CloseConnection ();
        Conn.CloseDevices ();
    }

    [Theory]
    [InlineData ( nameof ( InMemNetPoint ) )]
    [InlineData ( nameof ( IPNetPoint ) )]
    public void MultipleSend ( string type ) {
        var Conn = new BidirectionalConnection ( type );
        Conn.Connect ();

        // Send test message from devA to devB
        for ( int i = 0; i < 6; i++ ) {
            Conn.SendTo ( i );
            Conn.SendFrom ( i );
        }

        Conn.CloseConnection ();
        Conn.CloseDevices ();
    }

    [Theory]
    [InlineData ( nameof ( InMemNetPoint ) )]
    [InlineData ( nameof ( IPNetPoint ) )]
    public void ReconnectSame ( string type ) {
        var Conn = new BidirectionalConnection ( type );
        for ( int i = 0; i < 3; i++ ) {
            Conn.Connect ();

            Conn.SendTo ( i );
            Conn.SendFrom ( i );

            Conn.CloseConnection ();
        }
        Conn.CloseDevices ();
    }

    [Theory]
    [InlineData ( nameof ( InMemNetPoint ) )]
    [InlineData ( nameof ( IPNetPoint ) )]
    public void MultipleConnections ( string type ) {
        for ( int divI = 0; divI < 2; divI++ ) {
            NetConnectionGroup Conns = new ( type, 3, 3 );
            for ( int connI = 0; connI < 2; connI++ ) {
                Conns.Connect ();
				for ( int msgI = 0; msgI < 3; msgI++ ) {
                    int id = (divI << 6) | (connI << 4) + msgI;
                    Conns.SendTo ( id );
                    Conns.SendFrom ( id );
                }
                Conns.CloseConnection ();
            }
            Conns.CloseDevices ();
        }
    }
}

internal class NetworkCloseWatcher {
    public int ClosedCount { get; private set; } = 0;
    readonly INetDevice Device;
    readonly INetPoint Point;

    public NetworkCloseWatcher ( NetworkConnection conn ) {
        conn.OnClosed += OnClosed;
        Device = conn.LocalDevice;
        Point = conn.TargetEP;
    }

    private void OnClosed ( INetDevice dev, INetPoint ep ) {
        dev.Should ().BeSameAs ( Device );
        ep.Should ().BeSameAs ( Point );
        ClosedCount++;
    }

    public void Assert () {
        ClosedCount.Should ().Be ( 1, "because event OnClosed should be raised when closing Connection on both sides" );
    }
}