using System;
using System.Collections.Generic;
using System.Threading;

namespace InputResender.Services {
	public class CustomWaiter {
		public readonly string Name;
		public int Waiting { get; protected set; }
		private readonly List<WaiterReg> waiters;

		public CustomWaiter ( string name ) {
			Name = name;
			waiters = new List<WaiterReg> ();
		}

		public WaiterReg Wait () {
			var waiter = Register ();
			waiter.Wait ();
			return waiter;
		}
		public void Set () {
			lock ( waiters ) {
				foreach ( var waiter in waiters ) waiter.Set ();
			}
		}
		public WaiterReg Register () {
			var waiter = new WaiterReg ();
			lock ( waiters ) {
				waiters.Add ( waiter );
			}
			return waiter;
		}

		public class WaiterReg {
			public enum State { Created, Waiting, Raised, Cleared }
			public State ActState { get; protected set; }
			private readonly AutoResetEvent waiter;
			public WaiterReg () {
				waiter = new AutoResetEvent ( false );
				ActState = State.Created;
			}
			public void Wait () {
				lock ( waiter ) { ActState = State.Waiting; }
				waiter.WaitOne ();
				lock ( waiter ) { ActState = State.Cleared; }
			}
			public void Set () {
				lock ( waiter ) { ActState = State.Raised; }
				waiter.Set ();
			}
		}
		public class WaiterList {
			private readonly Dictionary<string, CustomWaiter> waiters;

			public WaiterList ( params string[] ssAr ) {
				waiters = new Dictionary<string, CustomWaiter> ();
				foreach ( string ss in ssAr ) Add ( ss );
			}

			public CustomWaiter Add ( string name ) {
				var ret = new CustomWaiter ( name );
				waiters.Add ( name, ret );
				return ret;
			}
			public void Wait ( string name ) => waiters[name].Wait ();
			public WaiterReg Register ( string name ) => waiters[name].Register ();
			public void Set ( string name ) => waiters[name].Set ();
			public void WaitAll () {
				int N = waiters.Count;
				var waitAr = new WaiterReg[N];
				int i = 0;
				foreach ( var kvp in waiters ) waitAr[i++] = kvp.Value.Register ();
				for ( i = 0; i < N; i++ ) waitAr[i].Wait ();
			}
			public void SetAll () { foreach ( var kvp in waiters ) kvp.Value.Set (); }
		}
	}
}