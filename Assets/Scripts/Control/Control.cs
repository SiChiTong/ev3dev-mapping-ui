﻿/*
 * Copyright (C) 2016 Bartosz Meglicki <meglickib@gmail.com>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 3 as
 * published by the Free Software Foundation.
 * This program is distributed "as is" WITHOUT ANY WARRANTY of any
 * kind, whether express or implied; without even the implied warranty
 * of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

/*
*  For functional requirements see:
*  https://github.com/bmegli/ev3dev-mapping-ui/issues/26
*  https://github.com/bmegli/ev3dev-mapping/issues/18
* 
*/

using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ControlProperties
{
	public int timeoutMs=1000;
	public bool autostart = true;
}

[RequireComponent (typeof (ControlUI))]
public class Control : ReplayableTCPClient<ControlMessage>
{
	public ControlProperties module;

	private List<RobotModule> modules=new List<RobotModule>();
	private ControlMessage msg=new ControlMessage();

	protected override void OnDestroy()
	{
		DisableModules();
		base.OnDestroy ();
	}

	protected override void Awake()
	{		
		base.Awake();
	}

	protected override void Start ()
	{
		modules.AddRange(transform.parent.GetComponentsInChildren<RobotModule>());
		modules.Remove(this);
		modules.Sort();
	}
				
	void Update ()
	{
		if (replay.ReplayInbound())
			return; 
		
		switch (GetState ())
		{
			case ModuleState.Offline:
				StartConnectingIfAutostartAndDisconnected ();
				break;
			case ModuleState.Initializing:
				StartConnectingIfAutostartAndDisconnected();
				if (TCPClientState == TCPClientState.Idle)
				{
					SetState(ModuleState.Failed);
					OutputErrorMessage();
				}
				if (TCPClientState == TCPClientState.Connected)
				{
					SetState (ModuleState.Online);

					if(!replay.ReplayOutbound())
						EnableModules();
				}
				break;
			case ModuleState.Online:
				if (TCPClientState == TCPClientState.Idle)
				{
					SetState(ModuleState.Failed);
					OutputErrorMessage();
					return;
				}
				while (ReceiveOne(msg))
					ProcessMessage(msg);
			
				if (LastSeen > module.timeoutMs)
					Send(msg.FillWithKeepaliveMessage());
				break;
			case ModuleState.Shutdown:
				while (ReceiveOne(msg))
					ProcessMessage(msg);
				if (AreAllModulesOfflineOrFailed())
				{ 
					Disconnect();
					SetState(ModuleState.Offline);
					print(name + " - disconnected");
				}
				break;
			case ModuleState.Failed:
				break;
		}

	}

	private void StartConnectingIfAutostartAndDisconnected()
	{		
		if (TCPClientState == TCPClientState.Disconnected && ModuleAutostart ())
		{
			StartConnecting ();
			SetState (ModuleState.Initializing);
		}
	}

	private void OutputErrorMessage()
	{
		print(name + " - failed - " + LastErrorMessage);
	}
		
	private ModuleState ModuleStateFromControlCommand(ControlCommands cmd)
	{
		if (cmd == ControlCommands.ENABLED)
			return ModuleState.Online;
		if (cmd == ControlCommands.DISABLED)
			return ModuleState.Offline;
		if (cmd == ControlCommands.FAILED)
			return ModuleState.Failed;
		throw new ArgumentException("Unable to convert command " + cmd.ToString() + " to ModuleState");	
	}

	private void ProcessMessage(ControlMessage message)
	{
		ControlCommands cmd = message.GetCommand();

		switch (cmd)
		{
			case ControlCommands.KEEPALIVE:
				break;
			case ControlCommands.ENABLED:				
			case ControlCommands.DISABLED:
			case ControlCommands.FAILED: //failed has additional information on error code
				RobotModule module = modules.Find(m => (m.name == message.Attribute<string>(0)));
				print(name + " - " + module.name + " state changed to " + cmd.ToString());
				module.SetState(ModuleStateFromControlCommand(cmd));
				break;
			default:
				print(name + " - ignoring unsupported command " + message.GetType());
				break;
		}
	}

	private bool AreAllModulesOfflineOrFailed()
	{
		foreach (RobotModule module in modules)
			if (!(module.GetState() == ModuleState.Offline || module.GetState() == ModuleState.Failed ) )
				return false;
		return true;
	}

	public void EnableDisableSelf(bool enable)
	{
		if(replay.ReplayInbound())
		{
			print(name + " - ignoring request to enable/disable " + name + " (replay)");
			return;
		}

		if (enable)
			EnableSelf();
		else
			DisableSelf();
	}

	private void DisableSelf()
	{
		if (GetState() != ModuleState.Online)
			return;

		SetState(ModuleState.Shutdown);	

		foreach (RobotModule module in modules)
			if(module.GetState() == ModuleState.Online || module.GetState() == ModuleState.Initializing)
				DisableModule(module);	
	}
	private void EnableSelf()
	{
		if (GetState() != ModuleState.Offline && GetState() != ModuleState.Failed)
			return;

		StartConnecting ();
		SetState (ModuleState.Initializing);
	}


	public void EnableDisableModule(string unique_module_name, bool enable)
	{
		if (replay.ReplayAny())
		{
			print(name + " - ignoring request to enable/disable " + unique_module_name + " (replay)");
			return;
		}

		RobotModule mod = modules.Find(m => (m.name == unique_module_name));

		if(enable)
			EnableModule(mod);
		else
			DisableModule(mod);

	}

	private void EnableModule(RobotModule m)
	{		
		m.SetState(ModuleState.Initializing);

		ControlMessage msg = ControlMessage.EnableMessage (m.name, m.ModuleCall (), (ushort)m.CreationDelayMs ());

		print(name + " - enable module " + m.name);
		Send (msg);

	}
		
	private void DisableModule(RobotModule m)
	{		
		m.SetState(ModuleState.Shutdown);

		ControlMessage msg = ControlMessage.DisableMessage(m.name);

		Send (msg);
		print(name + " - disable module " + m.name);
	}


	private void EnableModules()
	{		
		foreach (RobotModule module in modules)
		{
			if (!module.ModuleAutostart())
				continue;
		
			EnableModule(module);
		}
	}

	private void DisableModules()
	{ 
		if (GetState() != ModuleState.Online)
			return;

		foreach (RobotModule module in modules)
			if(module.GetState() == ModuleState.Initializing || module.GetState() == ModuleState.Online)
				module.SetState (ModuleState.Shutdown);

		ControlMessage msg = ControlMessage.DisableAllMessage();
		Send (msg);
		print(name + " - disable all modules");
	}

	#region RobotModule 

	//This module is exception, it controls other modules and has to be started on robot by other means (e.g. manually)
	//For now it's derived from ReplayableUDPClient and RobotModule but it's going to be rewritten for TCP/IP at some point

	public override string ModuleCall()
	{
		throw new NotImplementedException (name + " this should never happen");
	}
	public override int ModulePriority()
	{
		throw new NotImplementedException (name + " this should never happen");
	}
	public override bool ModuleAutostart()
	{
		return module.autostart;
	}
	public override int CreationDelayMs()
	{
		throw new NotImplementedException (name + " this should never happen");
	}

	#endregion
}