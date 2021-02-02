﻿using Microsoft.AspNetCore.Mvc;
using SmartProctor.Server.Services;
using SmartProctor.Server.Utils;
using SmartProctor.Shared.Requests;
using SmartProctor.Shared.Responses;

namespace SmartProctor.Server.Controllers.User
{
    [ApiController]
    [Route("api/user/[controller]")]
    public class RegisterController : ControllerBase
    {
        private readonly IUserServices _services;

        public RegisterController(IUserServices services)
        {
            _services = services;
        }

        [HttpPost]
        public BaseResponseModel Post(RegisterRequestModel model)
        {
            return ErrorCodes.CreateSimpleResponse(_services.Register(model.Id, model.NickName, model.Password,
                model.Email, model.Phone));
        }
    }
}