// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UnityUdpClient.cs">
//   Copyright (c) 2020 Johannes Deml. All rights reserved.
// </copyright>
// <author>
//   Johannes Deml
//   public@deml.io
// </author>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ENet;
using UnityEngine;
using Event = UnityEngine.Event;
using EventType = UnityEngine.EventType;

namespace NetCoreServer
{
	public class BenchmarkData
	{
		public int MessagesClientReceived;
		public int MessagesClientSent;
	}
	
	public class ENetClient
	{
		private Host _host;
		private Address _address;
		private Peer _peer;
		private int _messageLoad = 10;
		private int _tickRateClient = 60;
		private BenchmarkData _benchmarkData;

		public bool IsRunning;
		public bool IsConnected => _peer.State == PeerState.Connected;
		public Address Address => _address;
		public bool IsDisposed { get; private set; }
		private Task _listenTask;
		
		public ENetClient(BenchmarkData benchmarkData)
		{
			_benchmarkData = benchmarkData;
			ENet.Library.Initialize();
			_address = new Address();
			_host = new Host();
			IsDisposed = false;
		}

		public void Connect(string address, int port)
		{
			IsRunning = true;
			Debug.Log($"Connecting to host {_host.NativeData}");
			_host.Create();
			_address.SetHost(address);
			_address.Port = (ushort)port;
			_peer = _host.Connect(_address, 4);

			_listenTask = Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning);
			IsDisposed = false;
		}

		public void Send(byte[] message)
		{
			for (int i = 0; i < _messageLoad; i++)
			{
				SendUnreliable(message, 0, _peer);
			}
		}

		public void Disconnect()
		{
			_peer.DisconnectNow(0);
			IsRunning = false;
		}

		public async void Dispose()
		{
			while (!_listenTask.IsCompleted)
			{
				await Task.Delay(10);
			}
			_listenTask.Dispose();
			_host.Flush();
			_host.Dispose();
			ENet.Library.Deinitialize();
			IsDisposed = true;
		}

		private void Listen()
		{
			while (IsRunning) {
				_host.Service(1000 / _tickRateClient, out ENet.Event netEvent);

				switch (netEvent.Type) {
					case ENet.EventType.None:
						break;

					case ENet.EventType.Connect:
						Debug.Log("Client connected");
						break;

					case ENet.EventType.Receive:
						Interlocked.Increment(ref _benchmarkData.MessagesClientReceived);
						Debug.Log("Client received message!");

						netEvent.Packet.Dispose();

						break;
				}
			}
		}

		private void SendReliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = default(Packet);

			packet.Create(data, data.Length, PacketFlags.Reliable | PacketFlags.NoAllocate); // Reliable Sequenced
			peer.Send(channelID, ref packet);
			Interlocked.Increment(ref _benchmarkData.MessagesClientSent);
		}

		private void SendUnreliable(byte[] data, byte channelID, Peer peer) {
			Packet packet = default(Packet);

			packet.Create(data, data.Length, PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced
			peer.Send(channelID, ref packet);
			Interlocked.Increment(ref _benchmarkData.MessagesClientSent);
		}
	}
}
