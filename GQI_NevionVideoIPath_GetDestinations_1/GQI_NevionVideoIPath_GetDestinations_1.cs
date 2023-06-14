using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Analytics.GenericInterface;
using Skyline.DataMiner.Net.Messages;
using SLDataGateway.API.Types.Results.Paging;

[GQIMetaData(Name = "Nevion VideoIPath Get Destinations")]
public class GQI_NevionVideoIPath_GetDestinations : IGQIDataSource, IGQIOnInit, IGQIInputArguments
{
	private GQIDMS dms;

	private GQIStringArgument profileArgument = new GQIStringArgument("Profile") { IsRequired = false };
	private string profile;

	private GQIStringArgument tagArgument = new GQIStringArgument("Tags") { IsRequired = false };
	private string tag;

	private int dataminerId;
	private int elementId;

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("Name"),
			new GQIStringColumn("ID"),
			new GQIStringColumn("Description"),
			new GQIStringColumn("Tags"),
			new GQIStringColumn("Descriptor Label"),
			new GQIStringColumn("F Descriptor Label"),
		};
	}

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[] { profileArgument, tagArgument };
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		profile = args.GetArgumentValue<string>(profileArgument);
		tag = args.GetArgumentValue<string>(tagArgument);
		return new OnArgumentsProcessedOutputArgs();
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		if (dataminerId == -1 || elementId == -1)
		{
			return new GQIPage(new GQIRow[0])
			{
				HasNextPage = false,
			};
		}

		List<GQIRow> rows;
		if (!String.IsNullOrEmpty(tag))
		{
			rows = GetDestinationRows(tag);
		}
		else if (!String.IsNullOrEmpty(profile))
		{
			var tagFilter = GetTagsForProfile();
			rows = GetDestinationRows(tagFilter.ToArray());
		}
		else
		{
			rows = GetDestinationRows();
		}

		return new GQIPage(rows.ToArray())
		{
			HasNextPage = false,
		};
	}

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		dms = args.DMS;
		GetNevionVideoIPathElement();

		return new OnInitOutputArgs();
	}

	private void GetNevionVideoIPathElement()
	{
		dataminerId = -1;
		elementId = -1;

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
	}

	private HashSet<string> GetTagsForProfile()
	{
		var columns = GetProfilesTableColumns();
		if (!columns.Any())
		{
			return new HashSet<string>();
		}

		var profileTags = new HashSet<string>();

		for (int i = 0; i < columns[1].ArrayValue.Length; i++)
		{
			var profileNameCell = columns[1].ArrayValue[i];
			if (profileNameCell.IsEmpty)
			{
				continue;
			}

			var profileName = profileNameCell.CellValue.StringValue;
			if (profileName != profile)
			{
				continue;
			}

			var profileTagsCell = columns[3].ArrayValue[i];
			if (profileTagsCell.IsEmpty)
			{
				break;
			}

			var valueTags = profileTagsCell.CellValue.StringValue.Split(',');
			foreach (var valueTag in valueTags)
			{
				if (String.IsNullOrEmpty(valueTag))
				{
					continue;
				}

				profileTags.Add(valueTag.Trim());
			}

			break;
		}

		return profileTags;
	}

	private ParameterValue[] GetProfilesTableColumns()
	{
		var tableId = 2400;
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, tableId, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 4)
		{
			return new ParameterValue[0];
		}

		return columns;
	}

	private List<GQIRow> GetDestinationRows(params string[] tagFilter)
	{
		var columns = GetDestinationTableColumns();
		if (!columns.Any())
		{
			return new List<GQIRow>();
		}

		return ProcessDestinationTable(columns, tagFilter);
	}

	private ParameterValue[] GetDestinationTableColumns()
	{
		var getPartialTableMessage = new GetPartialTableMessage(dataminerId, elementId, 1400, new[] { "forceFullTable=true" });
		var parameterChangeEventMessage = (ParameterChangeEventMessage)dms.SendMessage(getPartialTableMessage);
		if (parameterChangeEventMessage.NewValue?.ArrayValue == null)
		{
			return new ParameterValue[0];
		}

		var columns = parameterChangeEventMessage.NewValue.ArrayValue;
		if (columns.Length < 6)
		{
			return new ParameterValue[0];
		}

		return columns;
	}

	private List<GQIRow> ProcessDestinationTable(ParameterValue[] columns, params string[] tagFilter)
	{
		var rows = new List<GQIRow>();

		for (int i = 0; i < columns[0].ArrayValue.Length; i++)
		{
			var nameCell = columns[0].ArrayValue[i];
			if (nameCell.IsEmpty)
			{
				continue;
			}

			var name = nameCell.CellValue.StringValue;

			var idCell = columns[1].ArrayValue[i];
			if (idCell.IsEmpty)
			{
				continue;
			}

			var id = idCell.CellValue.StringValue;

			var tagsCell = columns[3].ArrayValue[i];
			var tagsInCell = !tagsCell.IsEmpty ? tagsCell.CellValue.StringValue : String.Empty;

			if (tagFilter != null && tagFilter.Any())
			{
				var tagsForSource = tagsInCell.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim());
				if (!tagsForSource.Intersect(tagFilter).Any())
				{
					continue;
				}
			}

			var descriptionCell = columns[2].ArrayValue[i];
			var description = !descriptionCell.IsEmpty ? descriptionCell.CellValue.StringValue : String.Empty;

			var descriptorLabelCell = columns[4].ArrayValue[i];
			var descriptorLabel = !descriptorLabelCell.IsEmpty ? descriptorLabelCell.CellValue.StringValue : String.Empty;
			if (String.IsNullOrEmpty(descriptorLabel))
			{
				continue;
			}

			var fDescriptorLabelCell = columns[5].ArrayValue[i];
			var fDescriptorLabel = !fDescriptorLabelCell.IsEmpty ? fDescriptorLabelCell.CellValue.StringValue : String.Empty;

			var row = new GQIRow(
				new GQICell[]
				{
					new GQICell() { Value = name },
					new GQICell() { Value = id },
					new GQICell() { Value = description },
					new GQICell() { Value = tagsInCell },
					new GQICell() { Value = descriptorLabel },
					new GQICell() { Value = fDescriptorLabel },
				});
			rows.Add(row);
		}

		return rows;
	}
}