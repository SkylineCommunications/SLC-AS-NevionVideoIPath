using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;

[GQIMetaData(Name = "Nevion VideoIPath Get Tags")]
public class GQI_NevionVideoIPath_GetTags : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
	private GQIDMS dms;

	private GQIIntArgument typeArgument = new GQIIntArgument("Type") { IsRequired = true };
	private Type type;

	private enum Type
	{
		Source = 0,
		Destination = 1,
	}

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("Tag"),
		};
	}

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[] { typeArgument };
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		type = (Type)args.GetArgumentValue(typeArgument);
		return new OnArgumentsProcessedOutputArgs();
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		var tags = GetTags();

		var rows = new List<GQIRow>();
		foreach (var tag in tags.OrderBy(t => t))
		{
			var row = new GQIRow(new GQICell[] { new GQICell() { Value = tag } });
			rows.Add(row);
		}

		return new GQIPage(rows.ToArray())
		{
			HasNextPage = false,
		};
	}

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		dms = args.DMS;
		return new OnInitOutputArgs();
	}

	private HashSet<string> GetTags()
	{
		var tags = new HashSet<string>();

		int dataminerId = -1;
		int elementId = -1;

		var infoMessage = new GetInfoMessage { Type = InfoType.ElementInfo };
		var infoMessageResponses = dms.SendMessages(infoMessage);
		foreach (var response in infoMessageResponses)
		{
			var elementInfoEventMessage = (ElementInfoEventMessage)response;
			if (elementInfoEventMessage == null)
			{
				continue;
			}

			if (elementInfoEventMessage?.Protocol == "Nevion Video iPath" && elementInfoEventMessage?.ProtocolVersion == "Production")
			{
				dataminerId = elementInfoEventMessage.DataMinerID;
				elementId = elementInfoEventMessage.ElementID;
				break;
			}
		}

		if (dataminerId == -1 || elementId == -1)
		{
			return tags;
		}

		var tableId = type == Type.Source ? 1300 : 1400;
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return tags;
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 4)
		{
			return tags;
		}

		foreach (var tagsCell in columns[3].ArrayValue)
		{
			if (tagsCell.IsEmpty)
			{
				continue;
			}

			if (String.IsNullOrEmpty(tagsCell.CellValue.StringValue))
			{
				continue;
			}

			var valueTags = tagsCell.CellValue.StringValue.Split(',');
			foreach (var valueTag in valueTags)
			{
				if (String.IsNullOrEmpty(valueTag))
				{
					continue;
				}

				tags.Add(valueTag.Trim());
			}
		}

		return tags;
	}
}