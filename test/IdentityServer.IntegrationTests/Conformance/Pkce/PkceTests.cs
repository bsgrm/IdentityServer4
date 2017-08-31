﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using FluentAssertions;
using IdentityModel;
using IdentityModel.Client;
using IdentityServer4.IntegrationTests.Common;
using IdentityServer4.Models;
using IdentityServer4.Test;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IdentityServer4.IntegrationTests.Conformance.Pkce
{
    public class PkceTests
    {
        const string Category = "PKCE";

        IdentityServerPipeline _pipeline = new IdentityServerPipeline();

        Client client;

        const string client_id = "code_client";
        const string client_id_plain = "code_plain_client";
        const string client_id_pkce = "codewithproofkey_client";
        const string client_id_pkce_plain = "codewithproofkey_plain_client";


        string redirect_uri = "https://code_client/callback";
        string code_verifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string client_secret = "secret";
        string response_type = "code";

        public PkceTests()
        {
            _pipeline.Users.Add(new TestUser
            {
                SubjectId = "bob",
                Username = "bob",
                Claims = new Claim[]
                {
                        new Claim("name", "Bob Loblaw"),
                        new Claim("email", "bob@loblaw.com"),
                        new Claim("role", "Attorney")
                }
            });
            _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());

            _pipeline.Clients.Add(client = new Client
            {
                Enabled = true,
                ClientId = client_id,
                ClientSecrets = new List<Secret>
                {
                    new Secret(client_secret.Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                AllowedScopes = { "openid" },

                RequireConsent = false,
                RedirectUris = new List<string>
                {
                    redirect_uri
                }
            });
            _pipeline.Clients.Add(client = new Client
            {
                Enabled = true,
                ClientId = client_id_pkce,
                ClientSecrets = new List<Secret>
                {
                    new Secret(client_secret.Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                AllowedScopes = { "openid" },

                RequireConsent = false,
                RedirectUris = new List<string>
                {
                    redirect_uri
                }
            });

            // allow plain text PKCE
            _pipeline.Clients.Add(client = new Client
            {
                Enabled = true,
                ClientId = client_id_plain,
                ClientSecrets = new List<Secret>
                {
                    new Secret(client_secret.Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                AllowPlainTextPkce = true,

                AllowedScopes = { "openid" },

                RequireConsent = false,
                RedirectUris = new List<string>
                {
                    redirect_uri
                }
            });
            _pipeline.Clients.Add(client = new Client
            {
                Enabled = true,
                ClientId = client_id_pkce_plain,
                ClientSecrets = new List<Secret>
                {
                    new Secret(client_secret.Sha256())
                },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                AllowPlainTextPkce = true,

                AllowedScopes = { "openid" },

                RequireConsent = false,
                RedirectUris = new List<string>
                {
                    redirect_uri
                }
            });

            _pipeline.Initialize();
        }

        [Theory]
        [InlineData(client_id)]
        [InlineData(client_id_pkce)]
        [Trait("Category", Category)]
        public async Task Client_cannot_use_plain_code_challenge_method(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            _pipeline.ErrorWasCalled.Should().BeTrue();
            _pipeline.ErrorMessage.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Theory]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Client_can_use_plain_code_challenge_method(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri, code_verifier);

            tokenResponse.IsError.Should().BeFalse();
            tokenResponse.TokenType.Should().Be("Bearer");
            tokenResponse.AccessToken.Should().NotBeNull();
            tokenResponse.IdentityToken.Should().NotBeNull();
            tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(client_id)]
        [InlineData(client_id_pkce)]
        [Trait("Category", Category)]
        public async Task Client_can_use_sha256_code_challenge_method(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = Sha256OfCodeVerifier(code_verifier);
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Sha256);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri, code_verifier);

            tokenResponse.IsError.Should().BeFalse();
            tokenResponse.TokenType.Should().Be("Bearer");
            tokenResponse.AccessToken.Should().NotBeNull();
            tokenResponse.IdentityToken.Should().NotBeNull();
            tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(client_id_pkce)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Authorize_request_needs_code_challenge(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce);

            authorizeResponse.Should().BeNull();
        }

        [Theory]
        [InlineData(client_id)]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Authorize_request_code_challenge_cannot_be_too_short(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge:"a");

            _pipeline.ErrorWasCalled.Should().BeTrue();
            _pipeline.ErrorMessage.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Theory]
        [InlineData(client_id)]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Authorize_request_code_challenge_cannot_be_too_long(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: new string('a', _pipeline.Options.InputLengthRestrictions.CodeChallengeMaxLength + 1)
            );

            _pipeline.ErrorWasCalled.Should().BeTrue();
            _pipeline.ErrorMessage.Error.Should().Be(OidcConstants.AuthorizeErrors.InvalidRequest);
        }

        [Theory]
        [InlineData(client_id)]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Authorize_request_needs_supported_code_challenge_method(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: "unknown_code_challenge_method"
            );

            authorizeResponse.Should().BeNull();
        }

        [Theory]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Token_request_needs_code_verifier(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri);

            tokenResponse.IsError.Should().BeTrue();
            tokenResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidGrant);
        }

        [Theory]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Token_request_code_verifier_cannot_be_too_short(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri,
                "a");

            tokenResponse.IsError.Should().BeTrue();
            tokenResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidGrant);
        }

        [Theory]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Token_request_code_verifier_cannot_be_too_long(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri,
                new string('a', _pipeline.Options.InputLengthRestrictions.CodeVerifierMaxLength + 1));

            tokenResponse.IsError.Should().BeTrue();
            tokenResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidGrant);
        }

        [Theory]
        [InlineData(client_id_plain)]
        [InlineData(client_id_pkce_plain)]
        [Trait("Category", Category)]
        public async Task Token_request_code_verifier_must_match_with_code_chalenge(string clientId)
        {
            await _pipeline.LoginAsync("bob");

            var nonce = Guid.NewGuid().ToString();
            var code_challenge = code_verifier;
            var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId,
                response_type,
                IdentityServerConstants.StandardScopes.OpenId,
                redirect_uri,
                nonce: nonce,
                codeChallenge: code_challenge,
                codeChallengeMethod: OidcConstants.CodeChallengeMethods.Plain);

            authorizeResponse.IsError.Should().BeFalse();

            var code = authorizeResponse.Code;

            var tokenClient = new TokenClient(IdentityServerPipeline.TokenEndpoint, clientId, client_secret, _pipeline.Handler);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, redirect_uri,
                "mismatched_code_verifier");

            tokenResponse.IsError.Should().BeTrue();
            tokenResponse.Error.Should().Be(OidcConstants.TokenErrors.InvalidGrant);
        }

        private static string Sha256OfCodeVerifier(string codeVerifier)
        {
            var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hashedBytes = codeVerifierBytes.Sha256();
            var transformedCodeVerifier = Base64Url.Encode(hashedBytes);

            return transformedCodeVerifier;
        }
    }
}