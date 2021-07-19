// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Components;
using osu.Game.Screens.OnlinePlay.Lounge.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer;

namespace osu.Game.Tests.Visual.Multiplayer
{
    /// <summary>
    /// A <see cref="RoomManager"/> for use in multiplayer test scenes. Should generally not be used by itself outside of a <see cref="MultiplayerTestScene"/>.
    /// </summary>
    public class TestMultiplayerRoomManager : MultiplayerRoomManager
    {
        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private OsuGameBase game { get; set; }

        [Cached]
        public readonly Bindable<FilterCriteria> Filter = new Bindable<FilterCriteria>(new FilterCriteria());

        public new readonly List<Room> Rooms = new List<Room>();

        [BackgroundDependencyLoader]
        private void load()
        {
            int currentScoreId = 0;
            int currentRoomId = 0;
            int currentPlaylistItemId = 0;

            ((DummyAPIAccess)api).HandleRequest = req =>
            {
                switch (req)
                {
                    case CreateRoomRequest createRoomRequest:
                        var apiRoom = new Room();

                        apiRoom.CopyFrom(createRoomRequest.Room);
                        apiRoom.RoomID.Value ??= currentRoomId++;
                        for (int i = 0; i < apiRoom.Playlist.Count; i++)
                            apiRoom.Playlist[i].ID = currentPlaylistItemId++;

                        var responseRoom = new APICreatedRoom();
                        responseRoom.CopyFrom(createResponseRoom(apiRoom, false));

                        Rooms.Add(apiRoom);
                        createRoomRequest.TriggerSuccess(responseRoom);
                        return true;

                    case JoinRoomRequest joinRoomRequest:
                    {
                        var room = Rooms.Single(r => r.RoomID.Value == joinRoomRequest.Room.RoomID.Value);

                        if (joinRoomRequest.Password != room.Password.Value)
                        {
                            joinRoomRequest.TriggerFailure(new InvalidOperationException("Invalid password."));
                            return true;
                        }

                        joinRoomRequest.TriggerSuccess();
                        return true;
                    }

                    case PartRoomRequest partRoomRequest:
                        partRoomRequest.TriggerSuccess();
                        return true;

                    case GetRoomsRequest getRoomsRequest:
                        var roomsWithoutParticipants = new List<Room>();

                        foreach (var r in Rooms)
                            roomsWithoutParticipants.Add(createResponseRoom(r, false));

                        getRoomsRequest.TriggerSuccess(roomsWithoutParticipants);
                        return true;

                    case GetRoomRequest getRoomRequest:
                        getRoomRequest.TriggerSuccess(createResponseRoom(Rooms.Single(r => r.RoomID.Value == getRoomRequest.RoomId), true));
                        return true;

                    case GetBeatmapSetRequest getBeatmapSetRequest:
                        var onlineReq = new GetBeatmapSetRequest(getBeatmapSetRequest.ID, getBeatmapSetRequest.Type);
                        onlineReq.Success += res => getBeatmapSetRequest.TriggerSuccess(res);
                        onlineReq.Failure += e => getBeatmapSetRequest.TriggerFailure(e);

                        // Get the online API from the game's dependencies.
                        game.Dependencies.Get<IAPIProvider>().Queue(onlineReq);
                        return true;

                    case CreateRoomScoreRequest createRoomScoreRequest:
                        createRoomScoreRequest.TriggerSuccess(new APIScoreToken { ID = 1 });
                        return true;

                    case SubmitRoomScoreRequest submitRoomScoreRequest:
                        submitRoomScoreRequest.TriggerSuccess(new MultiplayerScore
                        {
                            ID = currentScoreId++,
                            Accuracy = 1,
                            EndedAt = DateTimeOffset.Now,
                            Passed = true,
                            Rank = ScoreRank.S,
                            MaxCombo = 1000,
                            TotalScore = 1000000,
                            User = api.LocalUser.Value,
                            Statistics = new Dictionary<HitResult, int>()
                        });
                        return true;
                }

                return false;
            };
        }

        private Room createResponseRoom(Room room, bool withParticipants)
        {
            var responseRoom = new Room();
            responseRoom.CopyFrom(room);
            responseRoom.Password.Value = null;
            if (!withParticipants)
                responseRoom.RecentParticipants.Clear();
            return responseRoom;
        }

        public new void ClearRooms() => base.ClearRooms();

        public new void Schedule(Action action) => base.Schedule(action);
    }
}
