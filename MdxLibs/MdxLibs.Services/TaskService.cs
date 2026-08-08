using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MdxLibs.Services {
	public class TaskService : IDisposable {
		private readonly Action<TaskService, ManualResetEvent> Fcn;
		private readonly List<string> StateList;
		private CancellationTokenSource CTS;
		private readonly AutoResetEvent SignalerIn, SignalerOut;
		private readonly ManualResetEvent InitWaiter;

		public Task ActTask { get; private set; }
		public string State {
			get { lock ( StateList ) { return StateList[0]; } }
			set { lock ( StateList ) { StateList.Insert ( 0, value ); } }
		}
		public CancellationToken CT => CTS.Token;
		public IReadOnlyList<string> History => StateList.AsReadOnly ();
		public bool ShouldStop => CTS.IsCancellationRequested;
		public bool IsSignaledFromOutside { get; private set; } = false;
		public bool IsSignaledFromInside { get; private set; } = false;
		public int AwatingForOutside { get; private set; } = 0;
		public int AwatingForInside { get; private set; } = 0;

		public TaskService ( Action<TaskService, ManualResetEvent> fcn ) {
			Fcn = fcn;
			CTS = new CancellationTokenSource ();
			StateList = new List<string> ();
			State = "Initializing";
			SignalerIn = new AutoResetEvent ( false );
			SignalerOut = new AutoResetEvent ( false );
			InitWaiter = new ManualResetEvent ( false );
		}
		public void Dispose () {
			Stop ();
			StateList.Clear ();
			CTS.Dispose ();
			SignalerIn.Dispose ();
			SignalerOut.Dispose ();
			InitWaiter.Dispose ();
		}

		public Task Start () {
			lock ( CTS ) {
				if ( ActTask != null ) return ActTask;
				ActTask = Task.Run ( () => Fcn ( this, InitWaiter ), CT );
				if ( !InitWaiter.WaitOne ( 60000 ) ) {
					CTS.Cancel ();
					Signal ( false );
					throw new TimeoutException ( "Task didn't send initialization signal. Trying to cancel created task." );
				}
				return ActTask;
			}
		}
		public void Stop () {
			lock ( CTS ) {
				if ( ActTask == null ) return;
				if ( ActTask.IsCompletedSuccessfully ) { DisposeTask (); return; }
				CTS.Cancel ();
			}
			while ( true ) {
				int waiting = 0;
				lock ( SignalerIn ) { waiting = AwatingForOutside; }
				if ( waiting < 1 ) break;
				for ( int i = 0; i < waiting; i++ ) Signal ( false );
				if ( ActTask.Wait ( 1 ) ) break;
			}
			lock ( CTS ) {
				ActTask.Wait ();
				DisposeTask ();
			}

			void DisposeTask () {
				ActTask.Dispose ();
				ActTask = null;
			}
		}
		public void Signal ( bool CalledWithinTask ) {
			if ( CalledWithinTask ) {
				lock (SignalerOut) { IsSignaledFromInside = true; }
				Thread.MemoryBarrier ();
				SignalerOut.Set ();
			} else {
				lock ( SignalerIn ) { IsSignaledFromOutside = true; }
				Thread.MemoryBarrier ();
				SignalerIn.Set ();
			}
		}
		public bool WaitSignal ( bool CalledWithinTask, int Timeout = int.MaxValue ) {
			if ( CalledWithinTask ) {
				lock ( SignalerOut ) { AwatingForInside++; }
				bool ret = SignalerOut.WaitOne ( Timeout );
				lock ( SignalerOut ) { AwatingForInside--; }
				return ret;
			} else {
				lock ( SignalerIn ) { AwatingForOutside++; }
				bool ret = SignalerIn.WaitOne ( Timeout );
				lock ( SignalerIn ) { AwatingForOutside--; }
				return ret;
			}
		}
	}
}