namespace LibraryBox.Web;

using System.Runtime.Serialization;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Fido2NetLib.Objects;

public record FidoCredentialEntity : ITableEntity
{
	public string? PartitionKey { get; set; }
	public string? RowKey { get; set; }
	public DateTimeOffset? Timestamp { get; set; }
	public ETag ETag { get; set; }
	public required byte[] Data { get; set; }
}

public record FidoCredential : ITableEntity
{
	public string? PartitionKey { get; set; }
	public string? RowKey { get; set; }

	public DateTimeOffset? Timestamp { get; set; }
	public ETag ETag { get; set; }

	/// <summary>
	/// The Credential ID of the public key credential source.
	/// </summary>
	public required byte[] Id { get; set; }

	/// <summary>
	/// The credential public key of the public key credential source.
	/// </summary>
	public required byte[] PublicKey { get; set; }

	/// <summary>
	/// The latest value of the signature counter in the authenticator data from any ceremony using the public key credential source.
	/// </summary>
	public int SignCount { get; set; }

	/// <summary>
	/// The value returned from getTransports() when the public key credential source was registered.
	/// </summary>
	public AuthenticatorTransport[]? Transports { get; set; }

	/// <summary>
	/// The value of the BE flag when the public key credential source was created.
	/// </summary>
	public bool IsBackupEligible { get; set; }

	/// <summary>
	/// The latest value of the BS flag in the authenticator data from any ceremony using the public key credential source.
	/// </summary>
	public bool IsBackedUp { get; set; }

	/// <summary>
	/// The value of the attestationObject attribute when the public key credential source was registered. 
	/// Storing this enables the Relying Party to reference the credential's attestation statement at a later time.
	/// </summary>
	public required byte[] AttestationObject { get; set; }

	/// <summary>
	/// The value of the clientDataJSON attribute when the public key credential source was registered. 
	/// Storing this in combination with the above attestationObject item enables the Relying Party to re-verify the attestation signature at a later time.
	/// </summary>
	public required byte[] AttestationClientDataJson { get; set; }

	public byte[]? DevicePublicKeysData { get; set; }
	List<byte[]> _devicePublicKeys = [];

	[IgnoreDataMember]
	public List<byte[]> DevicePublicKeys
	{
		get => _devicePublicKeys ??= JsonSerializer.Deserialize<List<byte[]>>(DevicePublicKeysData)!;
		set
		{
			_devicePublicKeys = value;
			DevicePublicKeysData = JsonSerializer.SerializeToUtf8Bytes(value);
		}
	}

	public required byte[] UserId { get; set; }

	public byte[]? DescriptorData { get; set; }
	PublicKeyCredentialDescriptor? _descriptor;

	[IgnoreDataMember]
	public required PublicKeyCredentialDescriptor Descriptor
	{
		get => _descriptor ??= JsonSerializer.Deserialize<PublicKeyCredentialDescriptor>(DescriptorData)!;
		set
		{
			_descriptor = value;
			DescriptorData = JsonSerializer.SerializeToUtf8Bytes(value);
		}
	}

	public required byte[] UserHandle { get; set; }

	public required string AttestationFormat { get; set; }

	public DateTimeOffset RegDate { get; set; }

	public Guid AaGuid { get; set; }
}