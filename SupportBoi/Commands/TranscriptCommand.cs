﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using System;
using System.Threading.Tasks;

namespace SupportBoi.Commands
{
    public class TranscriptCommand
    {
        [Command("transcript")]
        [Cooldown(1, 5, CooldownBucketType.User)]
        public async Task OnExecute(CommandContext command)
        {
            // Check if the user has permission to use this command.
            if (!Config.HasPermission(command.Member, "transcript"))
            {
                DiscordEmbed error = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Red,
                    Description = "You do not have permission to use this command."
                };
                await command.RespondAsync("", false, error);
                command.Client.DebugLogger.LogMessage(LogLevel.Info, "SupportBoi", "User tried to use the transcript command but did not have permission.", DateTime.UtcNow);
                return;
            }

            Database.Ticket ticket;
            string strippedMessage = command.Message.Content.Replace(Config.prefix, "");
            string[] parsedMessage = strippedMessage.Replace("<@!", "").Replace("<@", "").Replace(">", "").Split();

            // If there are no arguments use current channel
            if (parsedMessage.Length < 2)
            {
                if (Database.TicketLinked.TryGetOpenTicket(command.Channel.Id, out ticket))
                {
                    try
                    {
                        await Transcriber.ExecuteAsync(ticket.channelID.ToString(), ticket.id);
                    }
                    catch (Exception)
                    {
                        DiscordEmbed error = new DiscordEmbedBuilder
                        {
                            Color = DiscordColor.Red,
                            Description = "ERROR: Could not save transcript file. Aborting..."
                        };
                        await command.RespondAsync("", false, error);
                        throw;
                    }
                }
                else
                {
                    DiscordEmbed error = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Red,
                        Description = "This channel is not a ticket."
                    };
                    await command.RespondAsync("", false, error);
                    return;
                }
            }
            else
            {
                // Check if argument is numerical, if not abort
                if (!uint.TryParse(parsedMessage[1], out uint ticketID))
                {
                    DiscordEmbed error = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Red,
                        Description = "Argument must be a number."
                    };
                    await command.RespondAsync("", false, error);
                    return;
                }

                // If the ticket is still open, generate a new fresh transcript
                if (Database.TicketLinked.TryGetOpenTicketById(ticketID, out ticket) && ticket?.creatorID == command.Member.Id)
                {
                    try
                    {
                        await Transcriber.ExecuteAsync(ticket.channelID.ToString(), ticket.id);
                    }
                    catch (Exception)
                    {
                        DiscordEmbed error = new DiscordEmbedBuilder
                        {
                            Color = DiscordColor.Red,
                            Description = "ERROR: Could not save transcript file. Aborting..."
                        };
                        await command.RespondAsync("", false, error);
                        throw;
                    }

                }
                // If there is no open or closed ticket, send an error. If there is a closed ticket we will simply use the old transcript from when the ticket was closed.
                else if (!Database.TicketLinked.TryGetClosedTicketById(ticketID, out ticket) || (ticket?.creatorID != command.Member.Id && !Database.StaffLinked.IsStaff(command.Member.Id)))
                {
                    DiscordEmbed error = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Red,
                        Description = "Could not find a closed ticket with that number which you opened." + (Config.HasPermission(command.Member, "list") ? "\n(Use the " + Config.prefix + "list command to see all your tickets)" : "")
                    };
                    await command.RespondAsync("", false, error);
                    return;
                }
            }

            string filePath = Transcriber.GetPath(ticket.id);

            // Log it if the log channel exists
            DiscordChannel logChannel = command.Guild.GetChannel(Config.logChannel);
            if (logChannel != null)
            {
                DiscordEmbed logMessage = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Green,
                    Description = "Ticket " + ticket.id.ToString("00000") + " transcript generated by " + command.Member.Mention + ".\n",
                    Footer = new DiscordEmbedBuilder.EmbedFooter { Text = '#' + command.Channel.Name }
                };
                await logChannel.SendFileAsync(filePath, "", false, logMessage);
            }

            try
            {
                // Send transcript privately
                DiscordEmbed directMessage = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Green,
                    Description = "Transcript generated, " + command.Member.Mention + "!\n"
                };
                await command.Member.SendFileAsync(filePath, "", false, directMessage);

                // Respond to message directly
                DiscordEmbed response = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Green,
                    Description = "Transcript sent, " + command.Member.Mention + "!\n"
                };
                await command.RespondAsync("", false, response);
            }
            catch (UnauthorizedException)
            {
                // Send transcript privately
                DiscordEmbed error = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Red,
                    Description = "Not allowed to send direct message to you, " + command.Member.Mention + ", please check your privacy settings.\n"
                };
                await command.RespondAsync("", false, error);
            }
        }
    }
}
