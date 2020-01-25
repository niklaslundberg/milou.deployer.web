﻿using System.ComponentModel.DataAnnotations;
using Arbor.App.Extensions;
using Arbor.App.Extensions.Validation;
using MediatR;

namespace Milou.Deployer.Web.Core.Deployment.Messages
{
    public sealed class CreateOrganization : IRequest<CreateOrganizationResult>, IValidationObject
    {
        public CreateOrganization(string id)
        {
            Id = id;
        }

        [Required]
        public string Id { get; }

        public bool IsValid => Id.HasValue();
    }
}
