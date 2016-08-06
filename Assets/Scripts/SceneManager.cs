﻿using UnityEngine;
using System.Collections.Generic;

//SceneManager just for testing, rewrite needed
public class SceneManager : MonoBehaviour
{
	private Transform dynamicObjects;
	private GameObject uiCanvas;
	private GameObject robot;
	private Control robotControl;
	private GameObject modulesPanel;

	public static SceneManager Instance { get; private set; }

	public static Transform DynamicObjects
	{
		get {return Instance.dynamicObjects;}
	}

	public static GameObject ModulesPanel
	{
		get{return Instance.modulesPanel; }
	}

	private void Destroy()
	{
		Instance = null;
	}

	private void Awake()
	{
		if (Instance != null)
		{
			Debug.LogError("More than one SceneManager instance?");
			return;
		}
		Instance = this;
		dynamicObjects = new GameObject("DynamicObjects").transform;
		uiCanvas = GameObject.Find("UICanvas");
		robot = GameObject.FindGameObjectWithTag ("Player");
		robotControl = robot.GetComponent<Control> ();
		modulesPanel = GameObject.Find("ModulesPanel");
	}

	public void SaveMaps()
	{
		Laser[] lasers=FindObjectsOfType<Laser>();
		foreach (Laser l in lasers)
			l.SaveMap();
	}

	//temporary, change to something abstract for replays!
	//can be run only once or it will blow up!
	private bool replayStarted=false;
	public void StartReplay()
	{
		if (replayStarted)
			return;
		replayStarted = true;
		Control[] controls = FindObjectsOfType<Control> ();
		Odometry[] odometry = FindObjectsOfType<Odometry> ();
		Laser[] lasers=FindObjectsOfType<Laser>();
		Drive[] drives = FindObjectsOfType<Drive> ();

		foreach (Control c in controls)
			c.StartReplay ();
		foreach (Odometry o in odometry)
			o.StartReplay ();
		foreach (Laser l in lasers)
			l.StartReplay ();
		foreach (Drive d in drives)
			d.StartReplay ();

	}
	public void ToggleShowUI()
	{
		uiCanvas.SetActive(!uiCanvas.activeSelf);
	}
		
	public void EnableModules()
	{
		robotControl.EnableModules();
	}
	public void DisableModules()
	{		
		robotControl.DisableModules();
	}
}
	