using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace Radegast.Plugin.MapQuery
{
	[Radegast.Plugin(Name = "MapQuery Plugin", Description = "Map stuff.", Version = "1.0")]
	public class MapQueryPlugin : IRadegastPlugin
	{
		private RadegastInstance instance;
		private Dictionary<ulong, RegionData> regionCache = new Dictionary<ulong,RegionData>();
		private Dictionary<ulong, RegionData> regionCacheTemp = new Dictionary<ulong,RegionData>();
		private bool isUpdating = false;
		private readonly object regionCacheSync = new object();

		private List<string> Blacklist = new List<string>()
		{
			"Orsini Estate", // Security bot
			"LR 202", // Denied
			"LR 203", // Denied
			"LR 204", // Denied
			"LR 205", // Denied
			"LR 206", // Denied
			"LR 207", // Denied
			"LR 208", // Denied
			"LR 209", // Denied
			"LR 210", // Denied
			"LR 211", // Denied
			"LR 212", // Denied
		};
		private void Output(string msg)
		{
			instance.TabConsole.DisplayNotificationInChat("MapQuery: " + msg, ChatBufferTextStyle.StatusBlue);
		}

		public static ulong GlobalPosToRegionHandle(double globalX, double globalY)
		{
			uint x = ((uint)globalX / 256) * 256;
			uint y = ((uint)globalY / 256) * 256;

			return Utils.UIntsToLong(x, y);
		}

		private MySqlConnection db;

		const string cmdAddUpdate = "insert into updates (RegionHandle, Type, Name, Access, X, Y, AgentCount) values (@RegionHandle, @Type, @Name, @Access, @X, @Y, @AgentCount)";

		const string cmdAddRegion = "insert into region (RegionHandle, Name, Access, X, Y, AgentCount) values (@RegionHandle, @Name, @Access, @X, @Y, @AgentCount)";
		const string cmdDeleteRegion = "delete from region where RegionHandle = @RegionHandle";
		const string cmdUpdateRegion = "update region set Name = @Name, Access = @Access, X = @X, Y = @Y, AgentCount = @AgentCount where RegionHandle = @RegionHandle";
		const string cmdGetAllRegions = "SELECT * FROM region";

		class RegionTable
		{
			public ulong RegionHandle;
			public SimAccess Access;
			public string Name;
			public int X;
			public int Y;
			public int AgentCount;
			public bool StillExists;

			public bool IsEqualTo(RegionData rhs)
			{
				return this.RegionHandle == rhs.RegionHandle &&
					   this.Access == rhs.Access &&
					   this.Name == rhs.Name &&
					   this.X == rhs.X &&
					   this.Y == rhs.Y &&
					   this.AgentCount == rhs.AgentCount;
			}
		}

		private List<RegionTable> RegionList;
		private Dictionary<ulong, RegionTable> RegionMap;
		private List<RegionChange> RegionChanges;

		private Timer timerTick = new Timer(10000);
		private bool hasRegionDataChanged = false;

		private List<RegionTable> GetAllRegionsFromDatabase()
		{
			MySqlCommand cmd = new MySqlCommand(cmdGetAllRegions, db);

			List<RegionTable> regions = new List<RegionTable>();

			try
			{
				using (MySqlDataReader reader = cmd.ExecuteReader())
				{

					while (reader.Read())
					{
						RegionTable data = new RegionTable();

						data.RegionHandle = (ulong)((Int64)reader["RegionHandle"]);
						data.Access = (SimAccess)((sbyte)reader["Access"]);
						data.Name = (string)reader["Name"];
						data.X = (int)reader["X"];
						data.Y = (int)reader["Y"];
						data.AgentCount = (int)reader["AgentCount"];
						data.StillExists = false;

						regions.Add(data);
					}
				}
			}
			catch (Exception ex)
			{
				Output("Failed to get region: " + ex.Message);
				return null;
			}

			return regions;
		}

		private void AddRegion(RegionData newRegionData)
		{
			MySqlCommand cmd = new MySqlCommand(cmdAddRegion, db);
			cmd.Parameters.AddWithValue("RegionHandle",newRegionData.RegionHandle);
			cmd.Parameters.AddWithValue("Name", newRegionData.Name);
			cmd.Parameters.AddWithValue("Access", (sbyte)newRegionData.Access);
			cmd.Parameters.AddWithValue("X", newRegionData.X);
			cmd.Parameters.AddWithValue("Y", newRegionData.Y);
			cmd.Parameters.AddWithValue("AgentCount", newRegionData.AgentCount);


			int rowsAffected = 0;

			try
			{
				rowsAffected = cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Output("Failed to add region '" + newRegionData.Name + "': " + ex.Message);
			}
		}

		private void AddUpdate(RegionChange newRegionData)
		{
			//Output("Add update: " + newRegionData.Name);

			MySqlCommand cmd = new MySqlCommand(cmdAddUpdate, db);
			cmd.Parameters.AddWithValue("RegionHandle", newRegionData.RegionHandle);
			cmd.Parameters.AddWithValue("Type", newRegionData.Change);
			cmd.Parameters.AddWithValue("Name", newRegionData.Name);
			cmd.Parameters.AddWithValue("Access", (sbyte?)newRegionData.Access);
			cmd.Parameters.AddWithValue("X", newRegionData.X);
			cmd.Parameters.AddWithValue("Y", newRegionData.Y);
			cmd.Parameters.AddWithValue("AgentCount", newRegionData.AgentCount);

			int rowsAffected = 0;

			try
			{
				rowsAffected = cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Output("Failed to add region '" + newRegionData.Name + "': " + ex.Message);
			}
		}

		private void DeleteRegion(ulong regionHandle)
		{
			MySqlCommand cmd = new MySqlCommand(cmdDeleteRegion, db);
			cmd.Parameters.AddWithValue("RegionHandle", regionHandle);

			int rowsAffected = 0;

			try
			{
				rowsAffected = cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Output("Failed to delete region '" + RegionMap[regionHandle] + "': " + ex.Message);
			}


			if (rowsAffected == 0)
			{
				Output("Error: Failed to delete region");
			}
		}

		private void UpdateRegion(RegionData newRegionData)
		{
			MySqlCommand cmd = new MySqlCommand(cmdUpdateRegion, db);
			cmd.Parameters.AddWithValue("RegionHandle", newRegionData.RegionHandle);
			cmd.Parameters.AddWithValue("Name", newRegionData.Name);
			cmd.Parameters.AddWithValue("Access", (sbyte)newRegionData.Access);
			cmd.Parameters.AddWithValue("X", newRegionData.X);
			cmd.Parameters.AddWithValue("Y", newRegionData.Y);
			cmd.Parameters.AddWithValue("AgentCount", newRegionData.AgentCount);

			int rowsAffected = 0;

			try
			{
				rowsAffected = cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Output("Failed to update region: " + ex.Message);
			}

			if (rowsAffected == 0)
			{
				Output("Error: Failed to update region");
			}
		}

		private class RegionChange
		{
			public RegionChange()
			{
				X = null;
				Y = null;
				Access = null;
				Name = null;
				AgentCount = null;
			}

			public enum ChangeType
			{
				Add,
				Remove,
				Update
			}

			public ulong RegionHandle;
			public ChangeType Change;

			public int? X;
			public int? Y;
			public int? AgentCount;
			public SimAccess? Access;
			public string Name;
		}



		public void ProcessData(RegionData newRegionData)
		{
			if (RegionMap.ContainsKey(newRegionData.RegionHandle))
			{
				RegionMap[newRegionData.RegionHandle].StillExists = true;

				RegionTable previousData = RegionMap[newRegionData.RegionHandle];
				if (!previousData.IsEqualTo(newRegionData))
				{
					Output("Region update for: " + previousData.Name);

					RegionChange changed = new RegionChange();
					changed.RegionHandle = previousData.RegionHandle;
					changed.Change = RegionChange.ChangeType.Update;

					if (previousData.Name != newRegionData.Name)
					{
						Output("  Name: " + previousData.Name + " -> " + newRegionData.Name);
						changed.Name = newRegionData.Name;
					}
					if (previousData.Access != newRegionData.Access)
					{
						Output("  Access: " + previousData.Access + " -> " + newRegionData.Access);
						changed.Access = newRegionData.Access;
					}
					if (previousData.X != newRegionData.X)
					{
						Output("  X: " + previousData.X + " -> " + newRegionData.X);
						changed.X = newRegionData.X;
					}
					if (previousData.Y != newRegionData.Y)
					{
						Output("  Y: " + previousData.Y + " -> " + newRegionData.Y);
						changed.Y = newRegionData.Y;
					}
					if (previousData.AgentCount != newRegionData.AgentCount)
					{
						Output("  AgentCount: " + previousData.AgentCount + " -> " + newRegionData.AgentCount);
						changed.AgentCount = newRegionData.AgentCount;
					}

					UpdateRegion(newRegionData);
					RegionChanges.Add(changed);
				}
			}
			else
			{
				RegionChange changed = new RegionChange();
				changed.RegionHandle = newRegionData.RegionHandle;
				changed.Change = RegionChange.ChangeType.Add;
				RegionChanges.Add(changed);

				//Output("New region: " + newRegionData.Name);
				AddRegion(newRegionData);
			}
		}

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;
			db = new MySqlConnection(File.ReadAllText(@"..\..\db_MapQuery.ini"));
			try
			{
				db.Open();
			}
			catch (Exception ex)
			{
				Output("Failed to connect to DB: " + ex.Message);
				db = null;
				return;
			}

			Output("Running");
			instance = inst;

			inst.Client.Self.IM += Self_IM;
			inst.Client.Grid.GridItems += Grid_GridItems;
			inst.Client.Grid.GridRegion += Grid_GridRegion;
		}

		int currentRow = 0;
		private ushort stride = ushort.MaxValue;

		private void Update()
		{
			lock (regionCacheSync)
			{
				if (isUpdating)
					return;

				isUpdating = true;
			}

			Output("Update in progress!" );
			RegionList = GetAllRegionsFromDatabase();
			if (RegionList == null)
			{
				return;
			}
			RegionMap = RegionList.ToDictionary(k => k.RegionHandle);
			RegionChanges = new List<RegionChange>();

			regionCacheTemp = new Dictionary<ulong, RegionData>();


			instance.Client.Grid.RequestMapBlocks(GridLayerType.Objects, 0, (ushort)currentRow, ushort.MaxValue, (ushort)(currentRow + stride), false);
			currentRow += stride;

			timerTick.Elapsed += timerTick_Elapsed;
			timerTick.Start();
		}

		void Self_IM(object sender, InstantMessageEventArgs e)
		{
			Output("IM");
			if (e.IM.Dialog == InstantMessageDialog.MessageFromAgent // Message is not notice, inv. offer, etc etc
				&& !instance.Groups.ContainsKey(e.IM.IMSessionID) // Message is not group IM (sessionID == groupID)
				&& e.IM.BinaryBucket.Length < 2 // Session is not ad-hoc friends conference
				&& e.IM.FromAgentName != "Second Life" // Not a system message
				)
			{
				string lowerTrimmedMessage = e.IM.Message.ToLower().Trim();

				if (e.IM.FromAgentID == new UUID("24036859-e20e-40c4-8088-be6b934c3891"))
				{
					if (lowerTrimmedMessage == "update")
					{
						Update();
						return;
					}
					else if (lowerTrimmedMessage == "test")
					{
						return;
					}
				}
			}
		}

		void timerTick_Elapsed(object sender, ElapsedEventArgs e)
		{
			Output("Tick");
			lock (regionCacheSync)
			{
				if (hasRegionDataChanged)
				{
					hasRegionDataChanged = false;
					return;
				}

				if (currentRow < ushort.MaxValue)
				{
					Output("No changes detected since last tick, moving on to next request (" + currentRow + " / " + ushort.MaxValue + ")");

					timerTick.Start();
					instance.Client.Grid.RequestMapBlocks(GridLayerType.Objects, 0, (ushort)currentRow, ushort.MaxValue, (ushort)(currentRow + stride), false);
					currentRow += stride;
					return;
				}
				isUpdating = false;
				timerTick.Stop();
				Output("No changes detected since last tick, assuming we're done, total = " + regionCache.Count);

				regionCache = regionCacheTemp;
				foreach (var regionData in regionCache)
				{
					ProcessData(regionData.Value);
				}

				//var deletedRegions = (from x in RegionList where !x.StillExists select x).ToList();
				//Output("Total deleted: " + deletedRegions.Count);

				//foreach (var region in deletedRegions)
				//{
				//	RegionChange changed = new RegionChange();
				//	changed.RegionHandle = region.RegionHandle;
				//	changed.Change = RegionChange.ChangeType.Remove;
				//	RegionChanges.Add(changed);

				//	Output("Remove region: " + region.Name);

				//	DeleteRegion(region.RegionHandle);

				//}

				Output("Total changes: " + RegionChanges.Count);
				//foreach (var change in RegionChanges)
				//{
				//	AddUpdate(change);
				//}
			}

			Output("Done updating data :)");
		}

		public class RegionData
		{
			public ulong RegionHandle;
			public int X;
			public int Y;
			public SimAccess Access;
			public int AgentCount;
			public string Name;

			public override string ToString()
			{
				return string.Format("Name: {0} Rating: {1} Population: {2}", Name, Access, AgentCount);
			}
		}

		public void StopPlugin(RadegastInstance inst)
		{
			if (db == null)
			{
				return;
			}
			db.Close();

			inst.Client.Self.IM -= Self_IM;
			inst.Client.Grid.GridItems -= Grid_GridItems;
			inst.Client.Grid.GridRegion -= Grid_GridRegion;
		}

		void Grid_GridRegion(object sender, GridRegionEventArgs e)
		{
			lock (regionCacheSync)
			{
				hasRegionDataChanged = true;

				switch (e.Region.Access)
				{
					case SimAccess.Down:
					case SimAccess.NonExistent:
					case SimAccess.Unknown:
					case SimAccess.Trial:
						return;
				}

				if (!regionCacheTemp.ContainsKey(e.Region.RegionHandle))
				{
					regionCacheTemp.Add(e.Region.RegionHandle, new RegionData());
				}

				regionCacheTemp[e.Region.RegionHandle].RegionHandle = e.Region.RegionHandle;
				regionCacheTemp[e.Region.RegionHandle].Access = e.Region.Access;
				regionCacheTemp[e.Region.RegionHandle].Name = e.Region.Name;
				regionCacheTemp[e.Region.RegionHandle].X = e.Region.X;
				regionCacheTemp[e.Region.RegionHandle].Y = e.Region.Y;
			}

			instance.Client.Grid.RequestMapItems(e.Region.RegionHandle, GridItemType.AgentLocations, GridLayerType.Objects);
		}


		void Grid_GridItems(object sender, GridItemsEventArgs e)
		{
			lock (regionCacheSync)
			{
				hasRegionDataChanged = true;
			}

			int avatarCount = 0;
			if (e.Items.Count == 0)
			{
				return;
			}

			foreach (var mapItem in e.Items)
			{
				MapAgentLocation agentLocation = mapItem as MapAgentLocation;
				if (agentLocation == null)
				{
					continue;
				}

				avatarCount += agentLocation.AvatarCount;
			}

			ulong regionHandle = e.Items[0].RegionHandle;
			lock (regionCacheSync)
			{
				if (!regionCacheTemp.ContainsKey(regionHandle))
				{
					regionCacheTemp.Add(regionHandle, new RegionData());
				}

				regionCacheTemp[regionHandle].AgentCount = avatarCount;
			}
		}
	}
}