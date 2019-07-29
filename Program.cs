using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

// Import the EvidentProof API NuGet package services
using EvidentProof.API.Client.Services;

// Import the EvidentProof types.
using EvidentProof.Domain.Requests;
using EvidentProof.Domain.Responses;


namespace EvidentProof.API.Demo
{
	public class Program
	{
		public static async Task Main()
		{
			// Set up to use JSON settings file.
			var ConfigurationBuilder = new ConfigurationBuilder()
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json");

			var Configuration = ConfigurationBuilder.Build();

			// In a production app we'd validate these
			var ApiBaseUrl = Configuration["AppSettings:EvidenceApiEndpoint"];
			var ApiKey = Configuration["AppSettings:SampleApiKey"];
			var ServiceAgreementIdentifier = Configuration["AppSettings:ServiceAgreementIdentifier"];

			// We'll run the whole thing async.
			await RunSampleAsync(ApiBaseUrl, ServiceAgreementIdentifier, ApiKey);
		}

		private static async Task RunSampleAsync(string baseUrl, string serviceAgreementIdentifier, string apiKey)
		{
			// Collection of evidence to be stored as key value pairs of strings.
			// The same data will be submitted later for the validation proof certificate request.
			var Evidence = new Dictionary<string, string>
			{
				// First piece of evidence as a key value pair
				{
					"Phone Number", "+44 (0) 118 380 5520"
				},
				// Second piece of evidence
				{
					"Email", "enquiries@evident-proof.com"
				}
			};

			// Receipt for the evidence submission (This will be used later for the validation proof certificate request).
			Receipt EvidenceSubmissionReceipt;

			// Submit evidence.
			using (var EvidenceSubmissionService = new EvidenceSubmissionService(baseUrl, apiKey))
			{
				EvidenceSubmissionReceipt = await EvidenceSubmissionService.SubmitEvidenceAsync(
												serviceAgreementIdentifier,
												"Dispatch001",
												"Berkshire",
												DateTime.UtcNow,
												Evidence);

				// If the result is null then the evidence submission has failed.
				if (EvidenceSubmissionReceipt == null)
				{
					Console.WriteLine("Evidence submission failed!");
					return;
				}

				// Show the evidence submission receipt on the console.
				Console.WriteLine($"Evidence submission success!\n");

				ShowEvidenceSubmissionReceipt(EvidenceSubmissionReceipt);
			}

			// Generate both a validation and audit proof certificate for the previously submitted evidence.
			using (var ProofCertificateRequest = new ProofCertificateRequestService(baseUrl, apiKey))
			{
				// Create a single proof certificate receipt using the data returned from the evidence submission.
				var ProofCertificateReceipt = new ProofCertificateReceipt
				{
					Header = new ProofCertificateReceiptHeader
					{
						Id = EvidenceSubmissionReceipt.Id.ToString(),
						SourceSystemDispatchReference = EvidenceSubmissionReceipt.Header.SourceSystemDispatchReference,
						When = EvidenceSubmissionReceipt.Header.When,
						Where = EvidenceSubmissionReceipt.Header.Where
					},

					// List of evidence to "match" against previously stored evidence.
					Evidence = new List<ProofCertificateEvidence>
										{
                        // Note the same key value pair data entered during the evidence submission.
                        // This will yield a "pass" status when this request is compared to the previously stored evidence.
                        new ProofCertificateEvidence
												{
														Key = "Phone Number",
														Value = "+44 (0) 118 380 5520"
												},

												new ProofCertificateEvidence
												{
														Key = "Email",
														Value = "enquiries@evident-proof.com"
												}
										}
				};

				// Create an additional data object (The data therein will be present on the proof certificate itself). 
				// The passing of additional data is entirely optional. 
				// For this example, only the mandatory fields are supplied with data.
				var ProofCertificateAdditionalData = new ProofCertificateAdditionalDataRequest
				{
					ProofCertificateForTheAttentionOf = "Evident Proof",
					ProofCertificateDeliveredTo = "Evident Proof",
					ProofCertificateRequestedBy = "Evident Proof",

					DataOwners = "Evident Proof",
					DataOwnersContactDetails = "+ 44(0) 118 380 5520, enquiries@evident-proof.com",

					EventStatement = "Statement for the event concerned.",
					EventDefinitionStatement = "Statement definition for the event concerned."
				};

				// Make the request for a validation proof certificate. Supplying the additional data, as well as specifying that the proof certificates status should be set to "final".
				// A status of "draft" can also be specified, should there be a need for modification at a later date.
				var ValidationProofCertificateRequestResult = await ProofCertificateRequest.SubmitValidationProofCertificateRequest(
						serviceAgreementIdentifier,
						"Proof Certificate Requester Name",
						new List<ProofCertificateReceipt> { ProofCertificateReceipt },
						ProofCertificateAdditionalData,
						"final");

				// If the result is null then the validation proof certificate request has failed.
				if (ValidationProofCertificateRequestResult == null)
				{
					Console.WriteLine("Proof Certificate request failed!");
					return;
				}

				// Show the result of the request on the console.
				Console.WriteLine($"\nValidation Proof Certificate request success!\n");

				ShowProofCertificateResponse(ValidationProofCertificateRequestResult);

				// Make the request for an audit proof certificate. Supplying the additional data, as well as specifying that the proof certificates status should be set to "final".
				// A status of "draft" can also be specified, should there be a need for modification at a later date.
				var AuditProofCertificateRequestResult = await ProofCertificateRequest.SubmitAuditProofCertificateRequest(
						serviceAgreementIdentifier,
						"Proof Certificate Requester Name",
						EvidenceSubmissionReceipt.Header.SourceSystemDispatchReference,
						new List<string> { EvidenceSubmissionReceipt.Id.ToString() },
						ProofCertificateAdditionalData,
						"final");

				// If the result is null then the audit proof certificate request has failed.
				if (AuditProofCertificateRequestResult == null)
				{
					Console.WriteLine("Proof Certificate request failed!");
					return;
				}

				// Show the result of the request on the console.
				Console.WriteLine($"\nAudit Proof Certificate request success!\n");

				ShowProofCertificateResponse(AuditProofCertificateRequestResult);
			}

			using (var StatisticsService = new StatisticsService(baseUrl, apiKey))
			{
				var StatisticsResult = await StatisticsService.GetServiceAgreementStatistics(serviceAgreementIdentifier);

				if (StatisticsResult == null)
				{
					Console.WriteLine("Statistics request failed!");
					return;
				}

				ShowStatisticsResponse(StatisticsResult);
			}
		}

		private static void ShowProofCertificateResponse(ProofCertificateResponse proofCertificateResponse)
		{
			Console.WriteLine($"Proof Certificate Id: {proofCertificateResponse.ProofCertificateId}");
			Console.WriteLine($"Proof Certificate Url: {proofCertificateResponse.ProofCertificateUrl}\n");

			Console.WriteLine("Match Details:\n");

			foreach (var MatchDetail in proofCertificateResponse.MatchDetails)
			{
				Console.WriteLine(
								$"SourceSystemDispatchReference: {MatchDetail.SourceSystemDispatchReference}\n" +
								$"Key: {MatchDetail.Key}\n" +
								$"Value: {MatchDetail.Value}\n" +
								$"Match Status: {MatchDetail.MatchStatus}\n" +
								$"Status Reason: {MatchDetail.StatusReason}\n" +
								$"Timestamp: {MatchDetail.Timestamp}\n" +
								$"Order: {MatchDetail.Order}\n");
			}

			Console.WriteLine("Transaction Meta Data:\n");

			foreach (var TransactionMetaData in proofCertificateResponse.TransactionalMetaData)
			{
				Console.WriteLine(
								$"SourceSystemDispatchReference: {TransactionMetaData.SourceSystemDispatchReference}\n" +
								$"Ethereum Transaction Id: {TransactionMetaData.EthereumTransactionId}\n" +
								$"Key: {TransactionMetaData.Key}\n" +
								$"Seal: {TransactionMetaData.Seal}\n");
			}

			Console.WriteLine("Receipts Sent:\n");

			foreach (var SentReceipt in proofCertificateResponse.ReceiptsSent)
			{
				Console.WriteLine(
								$"Receipt Id: {SentReceipt.Id}\n" +
								$"Timestamp: {SentReceipt.Timestamp}\n" +
								$"Tokens: {SentReceipt.Tokens}\n");
			}
		}

		private static void ShowEvidenceSubmissionReceipt(Receipt receipt)
		{
			Console.WriteLine(
				$"Receipt Id: {receipt.Id}\n" +
				$"SourceSystemDispatchReference: {receipt.Header.SourceSystemDispatchReference}\n" +
				$"When: {receipt.Header.When}\n" +
				$"Where: {receipt.Header.Where}\n");

			Console.WriteLine("Evidence Details:\n");

			foreach (var EvidenceResult in receipt.Evidence)
			{
				Console.WriteLine(
					$"Evidence Id: {EvidenceResult.Id}\n" +
					$"Key: {EvidenceResult.Key}\n" +
					$"Seal: {EvidenceResult.Seal}\n" +
					$"EPT Storage Cost: {EvidenceResult.TokenFractionStorageCost}\n" +
					$"EPT Rewarded: {EvidenceResult.TokenFractionEarned}\n" +
					$"Seal Storage Band: {EvidenceResult.SealStorageBand}\n");
			}
		}

		private static void ShowStatisticsResponse(StatisticsResponse statisticsResponse)
		{
			Console.WriteLine(
				$"{nameof(statisticsResponse.ProofSealsStoredToday)}: {statisticsResponse.ProofSealsStoredToday}\n" +
				$"{nameof(statisticsResponse.ProofSealsStoredLast7Days)}: {statisticsResponse.ProofSealsStoredLast7Days}\n" +
				$"{nameof(statisticsResponse.ProofSealsStoredLast30Days)}: {statisticsResponse.ProofSealsStoredLast30Days}\n");

			Console.WriteLine(
				$"{nameof(statisticsResponse.ProofRequestsToday)}: {statisticsResponse.ProofRequestsToday}\n" +
				$"{nameof(statisticsResponse.ProofRequestsLast7Days)}: {statisticsResponse.ProofRequestsLast7Days}\n" +
				$"{nameof(statisticsResponse.ProofRequestsLast30Days)}: {statisticsResponse.ProofRequestsLast30Days}\n");

			Console.WriteLine(
				$"{nameof(statisticsResponse.ApiCalls)}: {statisticsResponse.ApiCalls}\n" +
				$"{nameof(statisticsResponse.ApiCallsToday)}: {statisticsResponse.ApiCallsToday}\n" +
				$"{nameof(statisticsResponse.ApiCallsLast7Days)}: {statisticsResponse.ApiCallsLast7Days}\n" +
				$"{nameof(statisticsResponse.ApiCallsLast30Days)}: {statisticsResponse.ApiCallsLast30Days}\n");

			Console.WriteLine(
				$"{nameof(statisticsResponse.ProofSealsStored)}: {statisticsResponse.ProofSealsStored}\n" +
				$"{nameof(statisticsResponse.ProofCertificates)}: {statisticsResponse.ProofCertificates}\n" +
				$"{nameof(statisticsResponse.ProofSealBundles)}: {statisticsResponse.ProofSealBundles}\n" +
				$"{nameof(statisticsResponse.ApiKeys)}: {statisticsResponse.ApiKeys}\n" +
				$"{nameof(statisticsResponse.HotWalletEptBalance)}: {statisticsResponse.HotWalletEptBalance}\n" +
				$"{nameof(statisticsResponse.PendingHotWalletBalance)}: {statisticsResponse.PendingHotWalletBalance}\n");
		}
	}
}
