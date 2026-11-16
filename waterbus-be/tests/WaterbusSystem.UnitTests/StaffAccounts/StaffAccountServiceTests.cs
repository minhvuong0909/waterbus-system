using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.StaffAccounts;
using WaterbusSystem.Application.DTOs.Responses.StaffAccounts;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Application.Validators.StaffAccounts;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.StaffAccounts;

/// <summary>Admin quản lý tài khoản Staff: mời kích hoạt, mã nhân viên duy nhất, không bật tài khoản chưa kích hoạt.</summary>
public class StaffAccountServiceTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private sealed class Harness
    {
        public readonly Mock<UserManager<ApplicationUser>> Users;
        public readonly Mock<IStaffAccountRepository> Repository = new();
        public readonly Mock<IPersonnelInvitationService> Invitations = new();
        public readonly StaffAccountService Sut;

        public Harness(string role = "Admin")
        {
            Users = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
            Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Staff")).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            Users.Setup(u => u.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            var current = new Mock<ICurrentUserService>();
            current.SetupGet(c => c.UserRole).Returns(role);
            current.SetupGet(c => c.UserId).Returns(AdminId.ToString());
            Sut = new StaffAccountService(Users.Object, Repository.Object, new DirectCommercialTransactionRunner(),
                current.Object, Invitations.Object);
        }

        public ApplicationUser Existing(Action<ApplicationUser>? change = null)
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "old@waterbus.vn", UserName = "old@waterbus.vn",
                FullName = "Nhân viên cũ", EmailConfirmed = true, IsActive = true, PasswordHash = "hash" };
            change?.Invoke(user);
            Repository.Setup(r => r.GetAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StaffAccountResponse(user.Id, user.FullName, user.Email!, null, user.StaffCode,
                    user.IsActive, user.PersonnelInvitationPending, user.EmailConfirmed));
            Users.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
            return user;
        }
    }

    private static CreateStaffAccountRequest Create(string? code = " st-01 ") =>
        new("Nguyễn Văn A", "a@waterbus.vn", "0901234567", code);

    [Fact]
    public async Task Create_ShouldMakeInactiveStaffWithPendingInvitationAndSendIt()
    {
        var h = new Harness();

        var id = await h.Sut.CreateAsync(Create());

        h.Users.Verify(u => u.CreateAsync(It.Is<ApplicationUser>(x => x.Id == id && x.Email == "a@waterbus.vn" &&
            x.StaffCode == "ST-01" && !x.IsActive && !x.EmailConfirmed && x.PersonnelInvitationPending && x.PasswordHash == null)), Times.Once);
        h.Users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Staff"), Times.Once);
        h.Invitations.Verify(i => i.SendAsync(id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ShouldNeverGrantAnyOtherRole()
    {
        var h = new Harness();

        await h.Sut.CreateAsync(Create());

        h.Users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.Is<string>(r => r != "Staff")), Times.Never);
    }

    [Fact]
    public async Task Create_WithoutStaffCode_ShouldNotCheckUniqueness()
    {
        var h = new Harness();

        await h.Sut.CreateAsync(Create(code: "  "));

        h.Users.Verify(u => u.CreateAsync(It.Is<ApplicationUser>(x => x.StaffCode == null)), Times.Once);
        h.Repository.Verify(r => r.StaffCodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithDuplicateStaffCode_ShouldConflictAndCreateNothing()
    {
        var h = new Harness();
        h.Repository.Setup(r => r.StaffCodeExistsAsync("ST-01", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => h.Sut.CreateAsync(Create());

        await act.Should().ThrowAsync<ConflictException>();
        h.Users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        h.Invitations.Verify(i => i.SendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenIdentityRejectsTheUser_ShouldThrowValidationAndSendNoInvitation()
    {
        var h = new Harness();
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Email đã dùng." }));

        var act = () => h.Sut.CreateAsync(Create());

        await act.Should().ThrowAsync<ValidationException>();
        h.Invitations.Verify(i => i.SendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Captain")]
    [InlineData("Passenger")]
    public async Task EveryOperation_ByNonAdmin_ShouldBeForbidden(string role)
    {
        var h = new Harness(role);
        var id = Guid.NewGuid();

        await FluentActions.Awaiting(() => h.Sut.CreateAsync(Create())).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Awaiting(() => h.Sut.ListAsync(new GetStaffAccountsRequest())).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Awaiting(() => h.Sut.ChangeStatusAsync(id, new ChangeStaffAccountStatusRequest(true))).Should().ThrowAsync<ForbiddenAccessException>();
        h.Users.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Update_WithSameEmail_ShouldKeepAccountActiveAndSendNoInvitation()
    {
        var h = new Harness();
        var user = h.Existing();

        await h.Sut.UpdateAsync(user.Id, new UpdateStaffAccountRequest("Tên mới", "OLD@waterbus.vn", "0901234567", "ST-9"));

        user.FullName.Should().Be("Tên mới");
        user.StaffCode.Should().Be("ST-9");
        user.IsActive.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();
        h.Invitations.Verify(i => i.SendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WithNewEmail_ShouldDeactivateUntilReactivatedAndInviteTheNewAddress()
    {
        var h = new Harness();
        var user = h.Existing();

        await h.Sut.UpdateAsync(user.Id, new UpdateStaffAccountRequest("Tên", "new@waterbus.vn", "0901234567"));

        user.Email.Should().Be("new@waterbus.vn");
        user.IsActive.Should().BeFalse();
        user.EmailConfirmed.Should().BeFalse();
        user.PersonnelInvitationPending.Should().BeTrue();
        h.Users.Verify(u => u.UpdateSecurityStampAsync(user), Times.Once); // vô hiệu link mời cũ
        h.Invitations.Verify(i => i.SendAsync(user.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WithStaffCodeOfAnotherAccount_ShouldConflict()
    {
        var h = new Harness();
        var user = h.Existing();
        h.Repository.Setup(r => r.StaffCodeExistsAsync("ST-9", user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => h.Sut.UpdateAsync(user.Id, new UpdateStaffAccountRequest("Tên", "old@waterbus.vn", "0901234567", "st-9"));

        await act.Should().ThrowAsync<ConflictException>();
        h.Users.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatus_ActivatingAnAccountThatNeverAcceptedTheInvitation_ShouldConflict()
    {
        var h = new Harness();
        var user = h.Existing(u => { u.IsActive = false; u.EmailConfirmed = false; u.PersonnelInvitationPending = true; u.PasswordHash = null; });

        var act = () => h.Sut.ChangeStatusAsync(user.Id, new ChangeStaffAccountStatusRequest(true));

        await act.Should().ThrowAsync<ConflictException>();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeStatus_ShouldDeactivateAndReactivateAnActivatedAccount()
    {
        var h = new Harness();
        var user = h.Existing();

        await h.Sut.ChangeStatusAsync(user.Id, new ChangeStaffAccountStatusRequest(false));
        user.IsActive.Should().BeFalse();

        await h.Sut.ChangeStatusAsync(user.Id, new ChangeStaffAccountStatusRequest(true));
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ResendInvitation_ForAnAccountThatIsNotStaff_ShouldNotSend()
    {
        var h = new Harness();
        var id = Guid.NewGuid();
        h.Repository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ThrowsAsync(new NotFoundException("Staff", id));

        var act = () => h.Sut.ResendInvitationAsync(id, "Gửi lại");

        await act.Should().ThrowAsync<NotFoundException>();
        h.Invitations.Verify(i => i.SendAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendInvitation_ForStaff_ShouldDelegateWithTheReason()
    {
        var h = new Harness();
        var user = h.Existing(u => { u.EmailConfirmed = false; u.PersonnelInvitationPending = true; });

        await h.Sut.ResendInvitationAsync(user.Id, "Chưa nhận được thư");

        h.Invitations.Verify(i => i.SendAsync(user.Id, "Chưa nhận được thư", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Validator ----------

    [Fact]
    public void CreateValidator_ShouldRequireNameEmailAndPhone()
    {
        var validator = new CreateStaffAccountRequestValidator();
        validator.TestValidate(new CreateStaffAccountRequest("", "not-an-email", "abc")).ShouldHaveValidationErrorFor(r => r.FullName);
        var bad = validator.TestValidate(new CreateStaffAccountRequest("A", "not-an-email", "abc"));
        bad.ShouldHaveValidationErrorFor(r => r.Email);
        bad.ShouldHaveValidationErrorFor(r => r.Phone);
        validator.TestValidate(Create()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ListValidator_ShouldBoundPaging()
    {
        var validator = new GetStaffAccountsRequestValidator();
        validator.TestValidate(new GetStaffAccountsRequest(0, 20)).ShouldHaveValidationErrorFor(r => r.Page);
        validator.TestValidate(new GetStaffAccountsRequest(1, 101)).ShouldHaveValidationErrorFor(r => r.PageSize);
        validator.TestValidate(new GetStaffAccountsRequest(1, 100)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ResendValidator_ShouldRequireAReason()
    {
        new ResendStaffInvitationRequestValidator().TestValidate(new ResendStaffInvitationRequest(" "))
            .ShouldHaveValidationErrorFor(r => r.Reason);
    }
}
