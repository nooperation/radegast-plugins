using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using OpenMetaverse;

namespace Radegast.Plugin.UriParserTest
{
    [Radegast.Plugin(Name = "UriParserTest Plugin", Description = "Testing", Version = "1.0")]
    public class UriParserTestPlugin : IRadegastPlugin
    {
        private RadegastInstance instance;
        private UriParser parser;

        public void StartPlugin(RadegastInstance inst)
        {
            instance = inst;
            parser = new UriParser(inst);
            instance.TabConsole.DisplayNotificationInChat("UriParserTest running", ChatBufferTextStyle.StatusBlue);
            inst.TabConsole.MainChatManger.ChatLineAdded += MainChatManger_ChatLineAdded;
        }

        public void StopPlugin(RadegastInstance inst)
        {
            inst.TabConsole.MainChatManger.ChatLineAdded -= MainChatManger_ChatLineAdded;
        }

        void MainChatManger_ChatLineAdded(object sender, ChatLineAddedArgs e)
        {
            if (parser.IsMatch(e.Item.Text))
            {
                e.Item.Text = parser.ParseInput(e.Item.Text);
               // instance.TabConsole.DisplayNotificationInChat(parser.ParseInput(e.Item.Text), ChatBufferTextStyle.StatusBlue);
            }
        }
    }


    class UriParser
    {
        readonly Regex patternUri = new Regex(
            @"(?<startingbrace>\[)?(" +
                @"(?<regionuri>secondlife://(?<region_name>[^\]/ ]+)(/(?<local_x>[0-9]+\.?[0-9]*)/(?<local_y>[0-9]+\.?[0-9]*)/(?<local_z>[0-9]+\.?[0-9]*))?)|" +
                @"(?<appuri>secondlife:///app/(" +
                    @"(?<appcommand>agent)/(?<agent_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/(?<action>[a-z]+)|" +
                    @"(?<appcommand>apperance)/show|" +
                    @"(?<appcommand>balance)/request|" +
                    @"(?<appcommand>chat)/(?<channel>\d+)/(?<text>[^\] ]+)|" + // NOTE: Channel must be > 0 and != DEBUG_CHANNEL
                    @"(?<appcommand>classified)/(?<classified_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/about|" +
                    @"(?<appcommand>event)/(?<event_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/about|" +
                    @"(?<appcommand>group)/(" +
                        @"(?<group_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/(?<action>[a-z]+)|" +
                        @"(?<action>create)|" +
                        @"(?<action>list/show))|" +
                    @"(?<appcommand>help)/?<help_query>([^\] ]+)|" +
                    @"(?<appcommand>inventory/(" +
                        @"(?<inventory_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/(?<action>select))|" +
                        @"(?<action>show))|" +
                    @"(?<appcommand>maptrackavatar)/(?<friend_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})|" +
                    @"(?<appcommand>objectim)/(?<object_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/?" +
                        @"([?&](" +
                            @"name=(?<name>[^& ]+)|" +
                            @"owner=(?<owner>[^& ]+)|" +
                            @"groupowned=(?<groupowned>true)|" +
                            @"slurl=(?<region_name>[^\]/ ]+)/(?<x>[0-9]+\.?[0-9]*)/(?<y>[0-9]+\.?[0-9]*)/(?<z>[0-9]+\.?[0-9]*)" +
                        @"))*|" +
                    @"(?<appcommand>parcel)/(?<parcel_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})/about|" +
                    @"(?<appcommand>search)/(?<category>[a-z]+)/(?<search_term>[^\]/ ]+)|" +
                    @"(?<appcommand>sharewithavatar)/(?<agent_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})|" +
                    @"(?<appcommand>teleport)/(?<region_name>[^\]/ ]+)(/(?<local_x>[0-9]+\.?[0-9]*)/(?<local_y>[0-9]+\.?[0-9]*)/(?<local_z>[0-9]+\.?[0-9]*))?|" +
                    @"(?<appcommand>voicecallavatar)/(?<agent_id>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})|" +
                    @"(?<appcommand>wear_folder)/?folder_id=(?<inventory_folder_uuid>[a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})|" +
                    @"(?<appcommand>worldmap)/(?<region_name>[^\]/ ]+)(/(?<local_x>[0-9]+\.?[0-9]*)/(?<local_y>[0-9]+\.?[0-9]*)/(?<local_z>[0-9]+\.?[0-9]*))?)))" +
            @"( (?<endingbrace>[^\]]*)\])?", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        readonly Regex patternUriSplit = new Regex(@"(\[secondlife://[^ \]]* ?(?:[^\]]*)])|(secondlife://[^ ]*)", RegexOptions.IgnoreCase);

        enum ResolveType
        {
            AgentCompleteName,
            AgentDisplayName,
            AgentUsername,
            Object,
            Classified,
            Event,
            Group,
            Parcel
        };

        private RadegastInstance instance;

        public UriParser(RadegastInstance instance)
        {
            this.instance = instance;
        }

        public bool IsMatch(string line)
        {
            return patternUriSplit.IsMatch(line);
        }

        public string ParseInput(string line)
        {
            StringBuilder sb = new StringBuilder();
            string[] split = patternUriSplit.Split(line);
            int i = 0;
            for (i = 0; i < split.Length - 1; i += 2)
            {
                sb.Append(split[i]);
                sb.Append(GetLinkName(split[i + 1], true));
            }
            if (i != split.Length)
            {
                sb.Append(split[i]);
            }

            return sb.ToString();
        }

        private const int NameResolveTimeoutInMs = 250;

        private string GetAgentName(string key, ResolveType nameType)
        {
            UUID agentID = new UUID(key);
            string name = null;

            using (ManualResetEvent gotName = new ManualResetEvent(false))
            {
                EventHandler<UUIDNameReplyEventArgs> handler = (object sender, UUIDNameReplyEventArgs e) =>
                {
                    if (e.Names.ContainsKey(agentID))
                    {
                        name = e.Names[agentID];
                        gotName.Set();
                    }
                };

                instance.Names.NameUpdated += handler;

                if (nameType == ResolveType.AgentCompleteName)
                {
                    name = instance.Names.GetLegacyName(agentID);
                }
                else if (nameType == ResolveType.AgentUsername)
                {
                    name = instance.Names.GetUserName(agentID);
                }
                else if (nameType == ResolveType.AgentDisplayName)
                {
                    name = instance.Names.GetDisplayName(agentID);
                }
                else
                {
                    return "[INVALID]";
                }

                if (name == RadegastInstance.INCOMPLETE_NAME)
                {
                    gotName.WaitOne(NameResolveTimeoutInMs, false);
                }

                instance.Names.NameUpdated -= handler;
            }

            return name;
        }

        private string GetGroupName(string key)
        {
            UUID groupID = new UUID(key);

            string name = RadegastInstance.INCOMPLETE_NAME;
            using (ManualResetEvent gotName = new ManualResetEvent(false))
            {

                EventHandler<GroupNamesEventArgs> handler = (object sender, GroupNamesEventArgs e) =>
                {
                    if (e.GroupNames.ContainsKey(groupID))
                    {
                        name = e.GroupNames[groupID];
                        gotName.Set();
                    }
                };

                instance.Client.Groups.GroupNamesReply += handler;
                instance.Client.Groups.RequestGroupName(groupID);
                if (name == RadegastInstance.INCOMPLETE_NAME)
                {
                    gotName.WaitOne(NameResolveTimeoutInMs, false);
                }

                instance.Client.Groups.GroupNamesReply -= handler;
            }

            return name;
        }


        string Resolve(string key, ResolveType type)
        {
            switch (type)
            {
                case ResolveType.AgentCompleteName:
                case ResolveType.AgentDisplayName:
                case ResolveType.AgentUsername:
                    return GetAgentName(key, type);
                case ResolveType.Group:
                    return GetGroupName(key);
                default:
                    return "{Resolved " + type + "}";
            }
            
        }

        public string GetLinkName(string uri, bool alwaysShowRealLink)
        {
            Match match = patternUri.Match(uri);
            if (!match.Success)
            {
                return uri;
            }

            if (match.Groups["startingbrace"].Success && match.Groups["endingbrace"].Length > 0)
            {
                string forcedLinkName = HttpUtility.UrlDecode(match.Groups["endingbrace"].Value);

                //if (alwaysShowRealLink)
                //{
                //    return "[" + forcedLinkName + " (" + GetRealLinkNamae(match) + ")]";
                //}

                return "[NamedLink: " + forcedLinkName + "]";
            }

            return GetRealLinkNamae(match);
        }

        private string GetRealLinkNamae(Match match)
        {
            if (match.Groups["regionuri"].Success)
            {
                return GetLinkNameRegionUri(match);
            }
            else if (match.Groups["appuri"].Success)
            {
                string appcommand = match.Groups["appcommand"].Value;

                switch (appcommand)
                {
                    case "agent":
                        return GetLinkNameAgent(match);
                    case "appearance":
                        return "[Show appearance]";
                    case "balance":
                        return "[Show balance]";
                    case "chat":
                        return GetLinkNameChat(match);
                    case "classified":
                        return GetLinkNameClassified(match);
                    case "event":
                        return GetLinkNameEvent(match);
                    case "group":
                        return GetLinkNameGroup(match);
                    case "help":
                        return GetLinkNameHelp(match);
                    case "inventory":
                        return GetLinkNameInventory(match);
                    case "maptrackavatar":
                        return GetLinkNameTrackAvatar(match);
                    case "objectim":
                        return GetLinkNameObjectIm(match);
                    case "parcel":
                        return GetLinkNameParcel(match);
                    case "search":
                        return GetLinkNameSearch(match);
                    case "sharewithavatar":
                        return GetLinkNameShareWithAvatar(match);
                    case "teleport":
                        return GetLinkNameTeleport(match);
                    case "voicecallavatar":
                        return GetLinkNameVoiceCallAvatar(match);
                    case "wear_folder":
                        return GetLinkNameWearFolder(match);
                    case "worldmap":
                        return GetLinkNameWorldMap(match);
                    default:
                        return match.ToString();
                }
            }

            return match.ToString();
        }

        private string GetLinkNameRegionUri(Match match)
        {
            string name = match.Groups["region_name"].Value;
            string x = match.Groups["local_x"].Value;
            string y = match.Groups["local_y"].Value;
            string z = match.Groups["local_z"].Value;

            return "[Show region '" + name + "']";
        }

        private string GetLinkNameAgent(Match match)
        {
            string uuid = match.Groups["agent_id"].Value;
            string action = match.Groups["action"].Value;

            switch (action)
            {
                case "completename":
                    return Resolve(uuid, ResolveType.AgentCompleteName);
                case "displayname":
                    return Resolve(uuid, ResolveType.AgentDisplayName);
                case "username":
                    return Resolve(uuid, ResolveType.AgentUsername);
                default:
                    return "[" + action + " '" + Resolve(uuid, ResolveType.AgentCompleteName) + "']";
            }
        }

        private string GetLinkNameChat(Match match)
        {
            string channel = match.Groups["channel"].Value;
            string text = System.Web.HttpUtility.UrlDecode(match.Groups["text"].Value);

            return "[Say on channel " + channel + ": " + text + "]";
        }

        private string GetLinkNameClassified(Match match)
        {
            string uuid = match.Groups["classified_id"].Value;
            return "[View classified '" + Resolve(uuid, ResolveType.Classified) + "']";
        }

        private string GetLinkNameEvent(Match match)
        {
            string uuid = match.Groups["event_id"].Value;
            return "[View event '" + Resolve(uuid, ResolveType.Event) + "'";
        }

        private string GetLinkNameGroup(Match match)
        {
            string uuid = match.Groups["group_id"].Value;
            string action = match.Groups["action"].Value;

            switch (action)
            {
                case "about":
                    return "[About group '" + Resolve(uuid, ResolveType.Group) + "']";
                case "inspect":
                    return "[Inspect group '" + Resolve(uuid, ResolveType.Group) + "']";
                case "create":
                    return "[Create group]";
                case "list/show":
                    return "[Show groups]";
            }

            return match.ToString();
        }

        private string GetLinkNameHelp(Match match)
        {
            string helpQuery = HttpUtility.UrlDecode(match.Groups["help_query"].Value);
            return "[Show help for '" + helpQuery + "']";
        }

        private string GetLinkNameInventory(Match match)
        {
            string uuid = match.Groups["inventory_id"].Value;
            string action = match.Groups["action"].Value;

            switch (action)
            {
                case "select":
                    return "[Select inventory (TODO)]";
                case "show":
                    return "[Show inventory]";
            }

            return match.ToString();
        }

        private string GetLinkNameTrackAvatar(Match match)
        {
            string uuid = match.Groups["friend_id"].Value;
            return "[Track '" + Resolve(uuid, ResolveType.AgentUsername) + "']";
        }

        private string GetLinkNameObjectIm(Match match)
        {
            string uuid = match.Groups["object_id"].Value;
            string name = HttpUtility.UrlDecode(match.Groups["name"].Value);
            string owner = match.Groups["owner"].Value;
            string groupowned = match.Groups["groupowned"].Value;
            string slurl = match.Groups["slurl"].Value;

            if (name != string.Empty)
            {
                return "[ObjectIM '" + name + "']";
            }

            return "[ObjectIM '" + Resolve(uuid, ResolveType.AgentUsername) + "']";
        }

        private string GetLinkNameParcel(Match match)
        {
            string uuid = match.Groups["parcel_id"].Value;
            return "[View parcel '" + Resolve(uuid, ResolveType.Parcel) + "']";
        }

        private string GetLinkNameSearch(Match match)
        {
            string category = match.Groups["category"].Value;
            string searchTerm = HttpUtility.UrlDecode(match.Groups["search_term"].Value);

            return "[Search for '" + searchTerm + "' in '" + category + "']";
        }

        private string GetLinkNameShareWithAvatar(Match match)
        {
            string uuid = match.Groups["agent_id"].Value;
            return "[Share with '" + Resolve(uuid, ResolveType.AgentUsername) + "'...]";
        }

        private string GetLinkNameTeleport(Match match)
        {
            string name = HttpUtility.UrlDecode(match.Groups["region_name"].Value);
            string x = match.Groups["local_x"].Value;
            string y = match.Groups["local_y"].Value;
            string z = match.Groups["local_z"].Value;

            return "[Teleport to '" + name + "']";
        }

        private string GetLinkNameVoiceCallAvatar(Match match)
        {
            string uuid = match.Groups["agent_id"].Value;
            return "[Voice call '" + Resolve(uuid, ResolveType.AgentUsername) + "']";
        }

        private string GetLinkNameWearFolder(Match match)
        {
            string uuid = match.Groups["inventory_folder_uuid"].Value;
            return "[Wear folder (caution!)]";
        }

        private string GetLinkNameWorldMap(Match match)
        {
            string name = HttpUtility.UrlDecode(match.Groups["region_name"].Value);
            string x = match.Groups["local_x"].Value;
            string y = match.Groups["local_y"].Value;
            string z = match.Groups["local_z"].Value;

            return "[Open map to region '" + name + "']";
        }
    }
}