/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace CancelService_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private static string[] primaryKeysCurrentServices = new string[0];
		private static Element nevionVideoIPathElement;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
		{
			try
			{
				var serviceIdParameter = engine.GetScriptParam("ServiceIds").Value;
				if (!TryGetIdsFromInput(serviceIdParameter, out List<string> serviceIds))
				{
					engine.ExitFail("Invalid service!");
					return;
				}

				nevionVideoIPathElement = engine.FindElementsByProtocol("Nevion Video iPath", "Production").FirstOrDefault();
				if (nevionVideoIPathElement == null)
				{
					engine.ExitFail("Nevion Video iPath element not found!");
					return;
				}

				primaryKeysCurrentServices = nevionVideoIPathElement.GetTablePrimaryKeys(1500); // Used to check if new connection entries has been added after the ConnectServices.

				CancelCurrentServices(engine, serviceIds);
				VerifyCancelService(engine, serviceIds);
			}
			catch (Exception e)
			{
				engine.Log($"Cancel failed: {e}");
				engine.ExitFail("Cancel failed due to unknown exception!");
			}
		}

		private static void CancelCurrentServices(IEngine engine, List<string> serviceIds)
		{
			var nevionVideoIPathElement = engine.FindElementsByProtocol("Nevion Video iPath", "Production").FirstOrDefault();
			if (nevionVideoIPathElement == null)
			{
				engine.ExitFail("Nevion Video iPath element not found!");
				return;
			}

			if (!nevionVideoIPathElement.IsActive)
			{
				engine.ExitFail("Nevion Video iPath element not active!");
				return;
			}

			foreach (var serviceId in serviceIds)
			{
				nevionVideoIPathElement.SetParameterByPrimaryKey(1515, serviceId, 1);
			}
		}

		private static bool TryGetIdsFromInput(string input, out List<string> ids)
		{
			ids = new List<string>();

			try
			{
				ids = JsonConvert.DeserializeObject<List<string>>(input);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static void VerifyCancelService(IEngine engine, List<string> serviceIds)
		{
			int retries = 0;
			bool allEntriesFound = false;
			int tableEntriesExcludingCurrentDestination = primaryKeysCurrentServices.Length - serviceIds.Count;
			while (!allEntriesFound && retries < 100)
			{
				engine.Sleep(60);

				var allPrimaryKeys = nevionVideoIPathElement.GetTablePrimaryKeys(1500);

				allEntriesFound = allPrimaryKeys.Length == tableEntriesExcludingCurrentDestination;

				retries++;
			}
		}
	}
}