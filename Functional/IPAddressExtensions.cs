using System;
using System.Net;

namespace PlayStudios.Functional
{
    /// <summary>
    /// Extension methods used with IP addresses.
    /// </summary>
    public static class IPAddressExtensions
    {
        /// <summary>
        /// Get the broadcast address for an IP address.
        /// </summary>
        /// <param name="address">An <see cref="IPAddress"/>.</param>
        /// <param name="subnetMask">An <see cref="IPAddress"/> representing the subnet mask.</param>
        /// <returns>An <see cref="IPAddress"/>. representing the broadcast address of the <paramref name="address"/>.</returns>
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        /// <summary>
        /// Get the network address for an IP address and a subnet.
        /// </summary>
        /// <param name="address">An <see cref="IPAddress"/>.</param>
        /// <param name="subnetMask">An <see cref="IPAddress"/> representing the subnet mask.</param>
        /// <returns>An <see cref="IPAddress"/>. representing the network address of the <paramref name="address"/>.</returns>
        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        /// <summary>
        /// Determines if two IP addresses are in the same subnet.
        /// </summary>
        /// <param name="address">The first <see cref="IPAddress"/>.</param>
        /// <param name="address2">The second <see cref="IPAddress"/>.</param>
        /// <param name="subnetMask">The subnet mask.</param>
        /// <returns>True if the addresses are in the same subnet, otherwise false.</returns>
        public static bool IsInSameSubnet(this IPAddress address, IPAddress address2, IPAddress subnetMask)
        {
            IPAddress network1 = address.GetNetworkAddress(subnetMask);
            IPAddress network2 = address2.GetNetworkAddress(subnetMask);

            return network1.Equals(network2);
        }
    }
}
