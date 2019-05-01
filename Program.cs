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
			var ApiKey = Configuration["AppSettings:SampleApiKey"]; ;
			var ServiceAgreementIdentifier = Configuration["AppSettings:ServiceAgreementIdentifier"]; ;

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

			// Receipt for the evidence submission (This will be used later for the proof certificate request).
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

				// Show the data on the console.
				Console.WriteLine(
						"Evidence submission success!\n\n" +
						$"Receipt Id: {EvidenceSubmissionReceipt.Id}\n" +
						$"SourceSystemDispatchReference: {EvidenceSubmissionReceipt.Header.SourceSystemDispatchReference}\n" +
						$"When: {EvidenceSubmissionReceipt.Header.When}\n" +
						$"Where: {EvidenceSubmissionReceipt.Header.Where}\n");

				Console.WriteLine("Evidence Details:\n");

				foreach (var EvidenceResult in EvidenceSubmissionReceipt.Evidence)
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

			// Generate a validation proof certificate for the previously submitted evidence.
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

				// Create a list of receipts and add the single receipt.
				var ProofCertificateReceipts = new List<ProofCertificateReceipt>
								{
										ProofCertificateReceipt
								};

				// Make the request.
				var ProofCertificateRequestResult = await ProofCertificateRequest.SubmitValidationProofCertificateRequest(
						serviceAgreementIdentifier,
						"Proof Certificate Requester Name",
						ProofCertificateReceipts);

				// If the result is null then the proof certificate request has failed.
				if (ProofCertificateRequestResult == null)
				{
					Console.WriteLine("Proof Certificate request failed!");
					return;
				}

				// Show the data on the console.
				Console.WriteLine($"\nProof Certificate request success!\n\nProof Certificate Id: {ProofCertificateRequestResult.ProofCertificateId}\n");

				Console.WriteLine("Match Details:\n");

				foreach (var MatchDetail in ProofCertificateRequestResult.MatchDetails)
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

				foreach (var TransactionMetaData in ProofCertificateRequestResult.TransactionalMetaData)
				{
					Console.WriteLine(
							$"SourceSystemDispatchReference: {TransactionMetaData.SourceSystemDispatchReference}\n" +
							$"Ethereum Transaction Id: {TransactionMetaData.EthereumTransactionId}\n" +
							$"Key: {TransactionMetaData.Key}\n" +
							$"Seal: {TransactionMetaData.Seal}\n");
				}

				Console.WriteLine("Receipts Sent:\n");

				foreach (var SentReceipt in ProofCertificateRequestResult.ReceiptsSent)
				{
					Console.WriteLine(
							$"Receipt Id: {SentReceipt.Id}\n" +
							$"Timestamp: {SentReceipt.Timestamp}\n" +
							$"Tokens: {SentReceipt.Tokens}\n");
				}
			}
		}
	}
}
