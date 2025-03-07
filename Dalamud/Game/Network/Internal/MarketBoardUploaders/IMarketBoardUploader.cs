using Dalamud.Game.Network.Structures;

namespace Dalamud.Game.Network.Internal.MarketBoardUploaders
{
    /// <summary>
    /// An interface binding for the Universalis uploader.
    /// </summary>
    internal interface IMarketBoardUploader
    {
        /// <summary>
        /// Upload data about an item.
        /// </summary>
        /// <param name="item">The item request data being uploaded.</param>
        void Upload(MarketBoardItemRequest item);

        /// <summary>
        /// Upload tax rate data.
        /// </summary>
        /// <param name="taxRates">The tax rate data being uploaded.</param>
        void UploadTax(MarketTaxRates taxRates);

        /// <summary>
        /// Upload information about a purchase this client has made.
        /// </summary>
        /// <param name="purchaseHandler">The purchase handler data associated with the sale.</param>
        void UploadPurchase(MarketBoardPurchaseHandler purchaseHandler);
    }
}
