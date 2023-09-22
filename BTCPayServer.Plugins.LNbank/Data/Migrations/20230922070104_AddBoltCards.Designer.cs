﻿// <auto-generated />
using System;
using BTCPayServer.Plugins.LNbank.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    [DbContext(typeof(LNbankPluginDbContext))]
    [Migration("20230922070104_AddBoltCards")]
    partial class AddBoltCards
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("BTCPayServer.Plugins.LNbank")
                .HasAnnotation("ProductVersion", "6.0.9")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.AccessKey", b =>
                {
                    b.Property<string>("Key")
                        .HasColumnType("text");

                    b.Property<string>("Level")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("Key");

                    b.HasIndex("WalletId", "UserId")
                        .IsUnique();

                    b.ToTable("AccessKeys", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Transaction", b =>
                {
                    b.Property<string>("TransactionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<long>("Amount")
                        .HasColumnType("bigint");

                    b.Property<long?>("AmountSettled")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExplicitStatus")
                        .HasColumnType("text");

                    b.Property<string>("InvoiceId")
                        .HasColumnType("text");

                    b.Property<bool>("IsSoftDeleted")
                        .HasColumnType("boolean");

                    b.Property<DateTimeOffset?>("PaidAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PaymentHash")
                        .HasColumnType("text");

                    b.Property<string>("PaymentRequest")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Preimage")
                        .HasColumnType("text");

                    b.Property<long?>("RoutingFee")
                        .HasColumnType("bigint");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.Property<string>("WithdrawConfigId")
                        .HasColumnType("text");

                    b.HasKey("TransactionId");

                    b.HasIndex("InvoiceId");

                    b.HasIndex("PaymentHash");

                    b.HasIndex("PaymentRequest");

                    b.HasIndex("WalletId");

                    b.HasIndex("WithdrawConfigId");

                    b.ToTable("Transactions", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", b =>
                {
                    b.Property<string>("WalletId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsSoftDeleted")
                        .HasColumnType("boolean");

                    b.Property<string>("LightningAddressIdentifier")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("PrivateRouteHintsByDefault")
                        .HasColumnType("boolean");

                    b.Property<string>("UserId")
                        .HasColumnType("text");

                    b.HasKey("WalletId");

                    b.HasIndex("UserId");

                    b.ToTable("Wallets", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.WithdrawConfig", b =>
                {
                    b.Property<string>("WithdrawConfigId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<bool>("IsSoftDeleted")
                        .HasColumnType("boolean");

                    b.Property<long?>("Limit")
                        .HasColumnType("bigint");

                    b.Property<long?>("MaxPerUse")
                        .HasColumnType("bigint");

                    b.Property<long?>("MaxTotal")
                        .HasColumnType("bigint");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ReuseType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("WalletId")
                        .HasColumnType("text");

                    b.HasKey("WithdrawConfigId");

                    b.HasIndex("WalletId");

                    b.ToTable("WithdrawConfigs", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Services.BoltCard", b =>
                {
                    b.Property<string>("BoltCardId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<string>("CardIdentifier")
                        .HasColumnType("text");

                    b.Property<int>("Counter")
                        .HasColumnType("integer");

                    b.Property<int?>("Index")
                        .HasColumnType("integer");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<string>("WithdrawConfigId")
                        .HasColumnType("text");

                    b.HasKey("BoltCardId");

                    b.HasIndex("WithdrawConfigId")
                        .IsUnique();

                    b.ToTable("BoltCards", "BTCPayServer.Plugins.LNbank");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.AccessKey", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("AccessKeys")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Transaction", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("Transactions")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.WithdrawConfig", "WithdrawConfig")
                        .WithMany()
                        .HasForeignKey("WithdrawConfigId");

                    b.Navigation("Wallet");

                    b.Navigation("WithdrawConfig");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.WithdrawConfig", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", "Wallet")
                        .WithMany("WithdrawConfigs")
                        .HasForeignKey("WalletId");

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Services.BoltCard", b =>
                {
                    b.HasOne("BTCPayServer.Plugins.LNbank.Data.Models.WithdrawConfig", "WithdrawConfig")
                        .WithOne("BoltCard")
                        .HasForeignKey("BTCPayServer.Plugins.LNbank.Services.BoltCard", "WithdrawConfigId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("WithdrawConfig");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.Wallet", b =>
                {
                    b.Navigation("AccessKeys");

                    b.Navigation("Transactions");

                    b.Navigation("WithdrawConfigs");
                });

            modelBuilder.Entity("BTCPayServer.Plugins.LNbank.Data.Models.WithdrawConfig", b =>
                {
                    b.Navigation("BoltCard");
                });
#pragma warning restore 612, 618
        }
    }
}