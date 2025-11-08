using Domain.Common;
using Domain.Events;

namespace Domain.Entities;

/// <summary>
/// Represents a user's address with associated phone numbers.
/// Supports CRUD operations with domain events for CQRS synchronization.
/// </summary>
public class Address : BaseEntity
{
    /// <summary>
    /// User who owns this address.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Street address line 1.
    /// </summary>
    public string Street { get; private set; } = string.Empty;

    /// <summary>
    /// Street address line 2 (apartment, suite, etc.).
    /// </summary>
    public string? Street2 { get; private set; }

    /// <summary>
    /// City name.
    /// </summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>
    /// State or province.
    /// </summary>
    public string State { get; private set; } = string.Empty;

    /// <summary>
    /// Postal or ZIP code.
    /// </summary>
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>
    /// Country name.
    /// </summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this is the default address for the user.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Label for this address (e.g., "Home", "Work", "Billing").
    /// </summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>
    /// Phone numbers associated with this address.
    /// </summary>
    private readonly List<Phone> _phones = new();
    public IReadOnlyCollection<Phone> Phones => _phones.AsReadOnly();

    /// <summary>
    /// Navigation property to User.
    /// </summary>
    public User? User { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Address() { }

    /// <summary>
    /// Creates a new address.
    /// Raises AddressCreatedEvent for CQRS synchronization.
    /// </summary>
    public static Address Create(
        Guid userId,
        string street,
        string city,
        string state,
        string postalCode,
        string country,
        string label = "Home",
        string? street2 = null,
        bool isDefault = false,
        List<string>? phoneNumbers = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street cannot be empty", nameof(street));

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty", nameof(city));

        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be empty", nameof(state));

        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code cannot be empty", nameof(postalCode));

        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty", nameof(country));

        var address = new Address
        {
            UserId = userId,
            Street = street,
            Street2 = street2,
            City = city,
            State = state,
            PostalCode = postalCode,
            Country = country,
            Label = label,
            IsDefault = isDefault
        };

        // Add phone numbers if provided
        if (phoneNumbers != null)
        {
            foreach (var phoneNumber in phoneNumbers)
            {
                address._phones.Add(Phone.Create(phoneNumber));
            }
        }

        // Raise domain event for synchronization
        address.AddDomainEvent(new AddressCreatedEvent
        {
            AddressId = address.Id,
            UserId = address.UserId,
            Street = address.Street,
            Street2 = address.Street2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            Label = address.Label,
            IsDefault = address.IsDefault,
            PhoneNumbers = address._phones.Select(p => p.Number).ToList(),
            CreatedAt = address.CreatedAt
        });

        return address;
    }

    /// <summary>
    /// Updates address details.
    /// Raises AddressUpdatedEvent for CQRS synchronization.
    /// </summary>
    public void Update(
        string? street = null,
        string? street2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? country = null,
        string? label = null,
        bool? isDefault = null)
    {
        if (street != null) Street = street;
        if (street2 != null) Street2 = street2;
        if (city != null) City = city;
        if (state != null) State = state;
        if (postalCode != null) PostalCode = postalCode;
        if (country != null) Country = country;
        if (label != null) Label = label;
        if (isDefault.HasValue) IsDefault = isDefault.Value;

        ModifiedAt = DateTime.UtcNow;

        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Sets this address as the default address.
    /// </summary>
    public void SetAsDefault()
    {
        IsDefault = true;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Unsets this address as default.
    /// </summary>
    public void UnsetAsDefault()
    {
        IsDefault = false;
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Adds a phone number to this address.
    /// </summary>
    public void AddPhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty", nameof(phoneNumber));

        // Check if phone already exists
        if (_phones.Any(p => p.Number == phoneNumber))
            return;

        _phones.Add(Phone.Create(phoneNumber));
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Removes a phone number from this address.
    /// </summary>
    public void RemovePhone(string phoneNumber)
    {
        var phone = _phones.FirstOrDefault(p => p.Number == phoneNumber);
        if (phone != null)
        {
            _phones.Remove(phone);
            ModifiedAt = DateTime.UtcNow;
            RaiseUpdatedEvent();
        }
    }

    /// <summary>
    /// Updates all phone numbers for this address.
    /// </summary>
    public void UpdatePhones(List<string> phoneNumbers)
    {
        _phones.Clear();
        foreach (var phoneNumber in phoneNumbers)
        {
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                _phones.Add(Phone.Create(phoneNumber));
            }
        }
        ModifiedAt = DateTime.UtcNow;
        RaiseUpdatedEvent();
    }

    /// <summary>
    /// Soft deletes the address.
    /// Raises AddressDeletedEvent for CQRS synchronization.
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        ModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new AddressDeletedEvent
        {
            AddressId = Id,
            UserId = UserId
        });
    }

    private void RaiseUpdatedEvent()
    {
        AddDomainEvent(new AddressUpdatedEvent
        {
            AddressId = Id,
            UserId = UserId,
            Street = Street,
            Street2 = Street2,
            City = City,
            State = State,
            PostalCode = PostalCode,
            Country = Country,
            Label = Label,
            IsDefault = IsDefault,
            PhoneNumbers = _phones.Select(p => p.Number).ToList(),
            ModifiedAt = ModifiedAt
        });
    }
}

/// <summary>
/// Value object representing a phone number.
/// Owned by Address entity.
/// </summary>
public class Phone
{
    /// <summary>
    /// Phone number.
    /// </summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Phone() { }

    /// <summary>
    /// Creates a new phone number.
    /// </summary>
    public static Phone Create(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Phone number cannot be empty", nameof(number));

        return new Phone { Number = number };
    }
}
