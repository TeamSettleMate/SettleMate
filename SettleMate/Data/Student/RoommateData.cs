using Microsoft.Data.SqlClient;
using SettleMate.Models.Student;

namespace SettleMate.Data.Student
{
    public class RoommateData
    {
        private readonly string connectionString;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public RoommateData(IConfiguration configuration)
        {
            connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is not configured.");
        }


        // =========================================================
        // GET ALL ROOMMATE LISTINGS
        // =========================================================

        public List<RoommateListingModel> GetListings(
            string? location,
            string? gender,
            decimal? minBudget,
            decimal? maxBudget)
        {
            List<RoommateListingModel> listings =
                new List<RoommateListingModel>();

            using SqlConnection connection =
                new SqlConnection(connectionString);

            string query = @"
                SELECT
                    RL.ListingID,
                    RL.StudentID,
                    RL.Gender,
                    RL.Location,
                    RL.Budget,
                    RL.LifestylePreferences,
                    RL.OtherListingInformation,
                    RL.ProfilePhoto,

                    U.FullName,
                    U.Email,
                    U.MobileNumber,
                    U.ProfileAvatar

                FROM RoommateListings RL

                INNER JOIN Users U
                    ON RL.StudentID = U.UserID

                WHERE
                    (@Location = ''
                     OR RL.Location LIKE '%' + @Location + '%')

                AND
                    (@Gender = ''
                     OR RL.Gender = @Gender)

                AND
                    (@MinBudget IS NULL
                     OR RL.Budget >= @MinBudget)

                AND
                    (@MaxBudget IS NULL
                     OR RL.Budget <= @MaxBudget)

                ORDER BY RL.ListingID DESC";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@Location",
                location ?? "");

            command.Parameters.AddWithValue(
                "@Gender",
                gender ?? "");


            SqlParameter minParameter =
                command.Parameters.AddWithValue(
                    "@MinBudget",
                    minBudget.HasValue
                        ? minBudget.Value
                        : DBNull.Value);

            minParameter.SqlDbType =
                System.Data.SqlDbType.Decimal;

            minParameter.Precision = 10;
            minParameter.Scale = 2;


            SqlParameter maxParameter =
                command.Parameters.AddWithValue(
                    "@MaxBudget",
                    maxBudget.HasValue
                        ? maxBudget.Value
                        : DBNull.Value);

            maxParameter.SqlDbType =
                System.Data.SqlDbType.Decimal;

            maxParameter.Precision = 10;
            maxParameter.Scale = 2;


            connection.Open();

            using SqlDataReader reader =
                command.ExecuteReader();


            while (reader.Read())
            {
                listings.Add(
                    new RoommateListingModel
                    {
                        ListingID =
                            Convert.ToInt32(
                                reader["ListingID"]),

                        StudentID =
                            Convert.ToInt32(
                                reader["StudentID"]),

                        Gender =
                            reader["Gender"]?.ToString() ?? "",

                        Location =
                            reader["Location"]?.ToString() ?? "",

                        Budget =
                            reader["Budget"] == DBNull.Value
                                ? null
                                : Convert.ToDecimal(
                                    reader["Budget"]),

                        LifestylePreferences =
                            reader["LifestylePreferences"]?.ToString()
                            ?? "",

                        OtherListingInformation =
                            reader["OtherListingInformation"]?.ToString()
                            ?? "",

                        ProfilePhoto =
                            reader["ProfilePhoto"]?.ToString()
                            ?? "",

                        FullName =
                            reader["FullName"]?.ToString()
                            ?? "",

                        Email =
                            reader["Email"]?.ToString()
                            ?? "",

                        MobileNumber =
                            reader["MobileNumber"]?.ToString()
                            ?? "",

                        ProfileAvatar =
                            reader["ProfileAvatar"]?.ToString()
                            ?? ""
                    });
            }

            return listings;
        }


        // =========================================================
        // GET ONE LISTING
        // =========================================================

        public RoommateListingModel? GetListing(int listingID)
        {
            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                SELECT
                    RL.ListingID,
                    RL.StudentID,
                    RL.Gender,
                    RL.Location,
                    RL.Budget,
                    RL.LifestylePreferences,
                    RL.OtherListingInformation,
                    RL.ProfilePhoto,

                    U.FullName,
                    U.Email,
                    U.MobileNumber,
                    U.ProfileAvatar

                FROM RoommateListings RL

                INNER JOIN Users U
                    ON RL.StudentID = U.UserID

                WHERE RL.ListingID = @ListingID";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@ListingID",
                listingID);


            connection.Open();


            using SqlDataReader reader =
                command.ExecuteReader();


            if (!reader.Read())
            {
                return null;
            }


            return new RoommateListingModel
            {
                ListingID =
                    Convert.ToInt32(
                        reader["ListingID"]),

                StudentID =
                    Convert.ToInt32(
                        reader["StudentID"]),

                Gender =
                    reader["Gender"]?.ToString() ?? "",

                Location =
                    reader["Location"]?.ToString() ?? "",

                Budget =
                    reader["Budget"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["Budget"]),

                LifestylePreferences =
                    reader["LifestylePreferences"]?.ToString()
                    ?? "",

                OtherListingInformation =
                    reader["OtherListingInformation"]?.ToString()
                    ?? "",

                ProfilePhoto =
                    reader["ProfilePhoto"]?.ToString()
                    ?? "",

                FullName =
                    reader["FullName"]?.ToString()
                    ?? "",

                Email =
                    reader["Email"]?.ToString()
                    ?? "",

                MobileNumber =
                    reader["MobileNumber"]?.ToString()
                    ?? "",

                ProfileAvatar =
                    reader["ProfileAvatar"]?.ToString()
                    ?? ""
            };
        }


        // =========================================================
        // GET STUDENT'S OWN LISTING
        // =========================================================

        public RoommateListingModel? GetMyListing(
            int studentID)
        {
            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                SELECT
                    RL.ListingID,
                    RL.StudentID,
                    RL.Gender,
                    RL.Location,
                    RL.Budget,
                    RL.LifestylePreferences,
                    RL.OtherListingInformation,
                    RL.ProfilePhoto,

                    U.FullName,
                    U.Email,
                    U.MobileNumber,
                    U.ProfileAvatar

                FROM RoommateListings RL

                INNER JOIN Users U
                    ON RL.StudentID = U.UserID

                WHERE RL.StudentID = @StudentID";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@StudentID",
                studentID);


            connection.Open();


            using SqlDataReader reader =
                command.ExecuteReader();


            if (!reader.Read())
            {
                return null;
            }


            return MapListing(reader);
        }


        // =========================================================
        // ADD LISTING
        // =========================================================

        public bool AddListing(
            RoommateListingModel model)
        {
            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                INSERT INTO RoommateListings
                (
                    StudentID,
                    Gender,
                    Location,
                    Budget,
                    LifestylePreferences,
                    OtherListingInformation,
                    ProfilePhoto
                )

                VALUES
                (
                    @StudentID,
                    @Gender,
                    @Location,
                    @Budget,
                    @LifestylePreferences,
                    @OtherListingInformation,
                    @ProfilePhoto
                )";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@StudentID",
                model.StudentID);

            command.Parameters.AddWithValue(
                "@Gender",
                model.Gender);

            command.Parameters.AddWithValue(
                "@Location",
                model.Location);


            SqlParameter budgetParameter =
                command.Parameters.AddWithValue(
                    "@Budget",
                    model.Budget.HasValue
                        ? model.Budget.Value
                        : DBNull.Value);

            budgetParameter.SqlDbType =
                System.Data.SqlDbType.Decimal;

            budgetParameter.Precision = 10;
            budgetParameter.Scale = 2;


            command.Parameters.AddWithValue(
                "@LifestylePreferences",
                string.IsNullOrWhiteSpace(
                    model.LifestylePreferences)
                    ? DBNull.Value
                    : model.LifestylePreferences);

            command.Parameters.AddWithValue(
                "@OtherListingInformation",
                string.IsNullOrWhiteSpace(
                    model.OtherListingInformation)
                    ? DBNull.Value
                    : model.OtherListingInformation);

            command.Parameters.AddWithValue(
                "@ProfilePhoto",
                string.IsNullOrWhiteSpace(
                    model.ProfilePhoto)
                    ? DBNull.Value
                    : model.ProfilePhoto);


            connection.Open();


            return command.ExecuteNonQuery() > 0;
        }


        // =========================================================
        // UPDATE LISTING
        // =========================================================

        public bool UpdateListing(
            RoommateListingModel model,
            int studentID)
        {
            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                UPDATE RoommateListings

                SET
                    Gender = @Gender,
                    Location = @Location,
                    Budget = @Budget,
                    LifestylePreferences =
                        @LifestylePreferences,
                    OtherListingInformation =
                        @OtherListingInformation,
                    ProfilePhoto = @ProfilePhoto

                WHERE
                    ListingID = @ListingID
                    AND StudentID = @StudentID";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@ListingID",
                model.ListingID);

            command.Parameters.AddWithValue(
                "@StudentID",
                studentID);

            command.Parameters.AddWithValue(
                "@Gender",
                model.Gender);

            command.Parameters.AddWithValue(
                "@Location",
                model.Location);


            SqlParameter budgetParameter =
                command.Parameters.AddWithValue(
                    "@Budget",
                    model.Budget.HasValue
                        ? model.Budget.Value
                        : DBNull.Value);

            budgetParameter.SqlDbType =
                System.Data.SqlDbType.Decimal;

            budgetParameter.Precision = 10;
            budgetParameter.Scale = 2;


            command.Parameters.AddWithValue(
                "@LifestylePreferences",
                string.IsNullOrWhiteSpace(
                    model.LifestylePreferences)
                    ? DBNull.Value
                    : model.LifestylePreferences);

            command.Parameters.AddWithValue(
                "@OtherListingInformation",
                string.IsNullOrWhiteSpace(
                    model.OtherListingInformation)
                    ? DBNull.Value
                    : model.OtherListingInformation);

            command.Parameters.AddWithValue(
                "@ProfilePhoto",
                string.IsNullOrWhiteSpace(
                    model.ProfilePhoto)
                    ? DBNull.Value
                    : model.ProfilePhoto);


            connection.Open();


            return command.ExecuteNonQuery() > 0;
        }


        // =========================================================
        // DELETE LISTING
        // =========================================================

        public bool DeleteListing(
            int listingID,
            int studentID)
        {
            using SqlConnection connection =
                new SqlConnection(connectionString);


            string query = @"
                DELETE FROM RoommateListings

                WHERE
                    ListingID = @ListingID
                    AND StudentID = @StudentID";


            using SqlCommand command =
                new SqlCommand(query, connection);


            command.Parameters.AddWithValue(
                "@ListingID",
                listingID);

            command.Parameters.AddWithValue(
                "@StudentID",
                studentID);


            connection.Open();


            return command.ExecuteNonQuery() > 0;
        }


        // =========================================================
        // MAP DATABASE RECORD
        // =========================================================

        private RoommateListingModel MapListing(
            SqlDataReader reader)
        {
            return new RoommateListingModel
            {
                ListingID =
                    Convert.ToInt32(
                        reader["ListingID"]),

                StudentID =
                    Convert.ToInt32(
                        reader["StudentID"]),

                Gender =
                    reader["Gender"]?.ToString() ?? "",

                Location =
                    reader["Location"]?.ToString() ?? "",

                Budget =
                    reader["Budget"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["Budget"]),

                LifestylePreferences =
                    reader["LifestylePreferences"]?.ToString()
                    ?? "",

                OtherListingInformation =
                    reader["OtherListingInformation"]?.ToString()
                    ?? "",

                ProfilePhoto =
                    reader["ProfilePhoto"]?.ToString()
                    ?? "",

                FullName =
                    reader["FullName"]?.ToString()
                    ?? "",

                Email =
                    reader["Email"]?.ToString()
                    ?? "",

                MobileNumber =
                    reader["MobileNumber"]?.ToString()
                    ?? "",

                ProfileAvatar =
                    reader["ProfileAvatar"]?.ToString()
                    ?? ""
            };
        }
    }
}