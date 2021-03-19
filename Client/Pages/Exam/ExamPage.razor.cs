﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.SignalR.Client;
using SmartProctor.Client.Components;
using SmartProctor.Client.Utils;
using SmartProctor.Client.WebRTCInterop;
using SmartProctor.Shared.Responses;
using SmartProctor.Shared.WebRTC;

namespace SmartProctor.Client.Pages.Exam
{
    public partial class ExamPage
    {
        [Parameter]
        public string ExamId { get; set; }

        private int examId;

        private int currentQuestionNum = 1;

        private ExamDetailsResponseModel examDetails;
        private WebRTCClientTaker _webRtcClient;
        private IList<string> _proctors;
        
        private HubConnection hubConnection;

        private bool localDesktopVideoLoaded = false;
        private bool localCameraVideoLoaded = false;

        private TestPrepareModal _testPrepareModal;
        private bool inPrepare = true;

        protected override async Task OnInitializedAsync()
        {
            examId = Int32.Parse(ExamId);
            if (await Attempt())
            {
                await GetExamDetails();
                await GetProctors();
                await SetupSignalRClient();
                SetupWebRTCClient();
                StateHasChanged();
            }
        }

        private async Task<bool> Attempt()
        {
            var result = await ExamServices.Attempt(examId);

            if (result == ErrorCodes.NotLoggedIn)
            {
                Modal.Error(new ConfirmOptions()
                {
                    Title = "You must login first",
                });
                NavManager.NavigateTo("/User/Login");
                return false;
            }
            else if (result != ErrorCodes.Success)
            {
                Modal.Error(new ConfirmOptions()
                {
                    Title = "Enter test failed",
                    Content = ErrorCodes.MessageMap[result]
                });
                NavManager.NavigateTo("/");
                return false;
            }

            return true;
        }

        private async Task GetExamDetails()
        {
            var details = await Http.GetFromJsonAsync<ExamDetailsResponseModel>("api/exam/ExamDetails/" + ExamId);

            if (details.Code == 0)
            {
                examDetails = details;
            }
        }

        private async Task GetProctors()
        {
            var (ret, proctors) = await ExamServices.GetProctors(examId);
            if (ret == ErrorCodes.Success)
            {
                _proctors = proctors;
            }
        }

        private void SetupWebRTCClient()
        {
            _webRtcClient = new WebRTCClientTaker(JsRuntime, _proctors.ToArray());
            
            _webRtcClient.OnProctorSdp += (_, e) =>
            {
                hubConnection.SendAsync("DesktopOffer", e.Item1, e.Item2);
            };

            _webRtcClient.OnProctorIceCandidate += (_, e) =>
            {
                hubConnection.SendAsync("SendDesktopIceCandidate", e.Item1, e.Item2);
            };

            _webRtcClient.OnCameraSdp += (_, sdp) =>
            {
                hubConnection.SendAsync("CameraAnswerFromTaker", sdp);
            };

            _webRtcClient.OnCameraIceCandidate += (_, candidate) =>
            {
                hubConnection.SendAsync("CameraIceCandidateToTaker", candidate);
            };
            
        }
        
        private async Task SetupSignalRClient()
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(NavManager.ToAbsoluteUri("/hub"))
                .Build();

            hubConnection.On<string>("ReceiveMessage",
                (message) =>
                {
                    // TODO: Process and display message
                });

            hubConnection.On<string, RTCSessionDescriptionInit>("ReceivedDesktopAnswer",
                async (proctor, sdp) =>
                {
                    await _webRtcClient.ReceivedProctorAnswerSDP(proctor, sdp);
                });

            hubConnection.On<string, RTCIceCandidate>("ReceivedDesktopIceCandidate",
                async (proctor, candidate) =>
                {
                    await _webRtcClient.ReceivedProctorIceCandidate(proctor, candidate);
                });
            hubConnection.On<RTCIceCandidate>("CameraIceCandidateToTaker",
                async candidate =>
                {
                    await _webRtcClient.ReceivedCameraIceCandidate(candidate);
                });
            hubConnection.On<RTCSessionDescriptionInit>("CameraOfferToTaker",
                async sdp =>
                {
                    await _webRtcClient.ReceivedCameraOfferSDP(sdp);
                });
            hubConnection.On<string>("ProctorConnected",
                async proctor =>
                {
                    await _webRtcClient.ReconnectToProctor(proctor);
                });

            await hubConnection.StartAsync();
        }

        private async Task OnDesktopVideoVisibleChange(bool visible)
        {
            if (visible && !localDesktopVideoLoaded)
            {
                await _webRtcClient.SetDesktopVideoElement("local-desktop");
                localDesktopVideoLoaded = true;
            }
        }

        private async Task OnCameraVideoVisibleChange(bool visible)
        {
            if (visible && !localCameraVideoLoaded)
            {
                await _webRtcClient.SetCameraVideoElement("local-camera");
                localCameraVideoLoaded = true;
            }
        }

        private async Task OnShareScreen()
        {
            var streamId = await _webRtcClient.ObtainDesktopStream();
            await _webRtcClient.SetDesktopVideoElement("desktop-video-dialog");
            if (_testPrepareModal.ShareScreenComplete(streamId))
            {
                await _webRtcClient.StartStreamingDesktop();
            }
        }

        private void OnPrepareFinish()
        {
            inPrepare = false;
        }

        private void OnNextQuestion()
        {
            ToQuestion(++currentQuestionNum);
        }

        private void OnPreviousQuestion()
        {
            ToQuestion(--currentQuestionNum);
        }

        private void ToQuestion(int index)
        {
            // TODO: Display questions
        }

        private void OnFinish()
        {
            Modal.Warning(new ConfirmOptions()
            {
                Title = "Time's up",
            });
        }
    }
}